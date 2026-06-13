using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;

namespace SecureStorage.Domain.Services;

/// <summary>
/// Service for managing secrets.
/// </summary>
public class SecretService(
    AppDbContext _dbContext,
    ILogger<SecretService> _logger,
    IOptionsSnapshot<AppSettings> _appSettings
) : ISecretService
{
    /// <summary>
    /// The name of the storage folder
    /// </summary>
    private const string StorageFolderName = "Storage";
    /// <summary>
    /// The time to live of the secret in hours
    /// </summary>
    private const double SecretTTLhHours = 48;
    /// <summary>
    /// The page size for secret summary pagination
    /// </summary>
    private const int PageSize = 20;

    /// <summary>
    /// Create a new secret
    /// 
    /// Arguments:
    /// - contentStream: The content of the secret
    /// - ownerId: The ID of the user who created the secret
    /// - comment: An optional comment for the secret
    /// - isOneTime: Whether the secret can only be used once
    /// - iv: The initialization vector for the secret
    /// - contentType: The content type of the secret
    /// - fileName: The file name of the secret
    /// 
    /// Returns:
    /// - The ID of the created secret
    /// 
    /// </summary>
    public async Task<Guid> CreateSecretAsync(Stream contentStream,
                                        Guid ownerId,
                                        string comment,
                                        bool isOneTime,
                                        byte[] iv,
                                        string contentType,
                                        string? fileName,
                                        long remainingQuotaBytes,
                                        CancellationToken ct)
    {
        Console.WriteLine($"[SecretService] CreateSecretAsync: ownerId = '{ownerId}'");
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == ownerId, ct);
        if (user == null)
        {
            Console.WriteLine($"[SecretService] CreateSecretAsync: User '{ownerId}' not found in database!");
            throw new InvalidOperationException("The user not found");
        }
        var secretId = Guid.CreateVersion7();

        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), StorageFolderName);
        if (!Directory.Exists(storagePath))
        {
            Directory.CreateDirectory(storagePath);
        }

        var filePath = Path.Combine(storagePath, secretId.ToString());
        try
        {
            using (var fileStream = File.Create(filePath))
            {
                var buffer = new byte[81920]; // 80KB chunk
                long totalReadBytes = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    totalReadBytes += read;
                    if (totalReadBytes > remainingQuotaBytes)
                    {
                        fileStream.Close();
                        File.Delete(filePath);
                        throw new InvalidOperationException("Storage quota exceeded.");
                    }
                    await fileStream.WriteAsync(buffer, 0, read, ct);
                }
            }

            var size = new FileInfo(filePath).Length;

            var isInMemory = _dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
            if (isInMemory)
            {
                await SaveSecretDbRecordAsync(secretId, ownerId, comment, isOneTime, iv, contentType, fileName, size, ct);
            }
            else
            {
                var strategy = _dbContext.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                    try
                    {
                        await SaveSecretDbRecordAsync(secretId, ownerId, comment, isOneTime, iv, contentType, fileName, size, ct);
                        await transaction.CommitAsync(ct);
                    }
                    catch
                    {
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
                });
            }

            return secretId;
        }
        catch (Exception)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            throw;
        }
    }

    private async Task SaveSecretDbRecordAsync(Guid secretId, Guid ownerId, string comment, bool isOneTime, byte[] iv, string contentType, string? fileName, long size, CancellationToken ct)
    {
        // Detach existing secret entity if it was already added in a failed/retried attempt
        var existingSecret = _dbContext.ChangeTracker.Entries<Secret>().FirstOrDefault(e => e.Entity.Id == secretId);
        if (existingSecret != null)
        {
            existingSecret.State = EntityState.Detached;
        }

        var quota = await _dbContext.UserQuota.FirstOrDefaultAsync(q => q.UserId == ownerId, ct);
        if (quota == null)
        {
            quota = new UserQuota
            {
                UserId = ownerId,
                Quota = _appSettings.Value.QuotaBytes,
                UsedQuota = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.UserQuota.Add(quota);
        }

        var limit = quota.Quota > 0 ? quota.Quota : _appSettings.Value.QuotaBytes;
        if (quota.UsedQuota + size > limit)
        {
            throw new InvalidOperationException("Storage quota exceeded.");
        }

        quota.UsedQuota += size;
        quota.UpdatedAt = DateTime.UtcNow;

        var secret = new Secret
        {
            Id = secretId,
            OwnerId = ownerId,
            Comment = comment ?? string.Empty,
            IsOneTime = isOneTime,
            IsBurned = false,
            ContentType = contentType,
            FileName = fileName,
            IV = iv,
            Size = size,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(SecretTTLhHours)
        };

        _dbContext.Secrets.Add(secret);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Get a secret by ID
    /// 
    /// Arguments:
    /// - secretId: The ID of the secret to get
    /// - currentUserId: The ID of the user who wants to get the secret
    /// 
    /// Returns:
    /// - The secret if found
    /// 
    /// </summary>
    public async Task<SecretDto?> GetSecretAsync(Guid secretId, Guid? currentUserId, CancellationToken ct)
    {
        var secret = await _dbContext.Secrets.FirstOrDefaultAsync(s => s.Id == secretId
                                                                       && s.ExpiresAt.CompareTo(DateTime.UtcNow) > 0
                                                                       && !s.IsBurned,
                                                                   ct);
        if (secret == null)
        {
            return null;
        }
        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), StorageFolderName);
        var filePath = Path.Combine(storagePath, secretId.ToString());
        var buffer = await File.ReadAllBytesAsync(filePath, ct);

        // Burn one-time secret ONLY if the accessor is NOT the owner
        if (secret.IsOneTime && secret.OwnerId != currentUserId)
        {
            await BurnSecretAsync(secret.Id, secret.OwnerId, ct);
        }

        return new SecretDto(
            buffer,
            secret.IV,
            secret.ContentType,
            secret.FileName,
            secret.IsOneTime
        );
    }

    /// <summary>
    /// Get all secrets for a user
    /// 
    /// Arguments:
    /// - ownerId: The ID of the user to get secrets for
    /// - lastSecretId: The ID of the last secret to get secrets for
    /// - ct: The cancellation token
    /// 
    /// Returns:
    /// - The list of secrets for the user
    /// 
    /// </summary>
    public async Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId, Guid? lastSecretId, CancellationToken ct)
    {
        var query = _dbContext.Secrets
            .Where(s => s.OwnerId == ownerId && !s.IsBurned && s.ExpiresAt > DateTime.UtcNow);

        if (lastSecretId.HasValue)
        {
            query = query.Where(s => s.Id < lastSecretId.Value);
        }

        return await query
            .OrderByDescending(s => s.Id)
            .Take(PageSize)
            .Select(s => new SecretSummaryDto(
                s.Id,
                s.Comment,
                s.IsOneTime,
                s.Size,
                s.CreatedAt
            ))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Burn a secret (mark as used)
    /// 
    /// Arguments:
    /// - secretId: The ID of the secret to burn
    /// - ownerId: The ID of the user who owns the secret
    /// 
    /// </summary>
    public async Task BurnSecretAsync(Guid secretId, Guid ownerId, CancellationToken ct)
    {
        var isInMemory = _dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        if (isInMemory)
        {
            await ExecuteBurnSecretAsync(secretId, ownerId, ct);
        }
        else
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                try
                {
                    await ExecuteBurnSecretAsync(secretId, ownerId, ct);
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
        }
    }

    private async Task ExecuteBurnSecretAsync(Guid secretId, Guid ownerId, CancellationToken ct)
    {
        var secret = await _dbContext.Secrets.FirstOrDefaultAsync(s => s.Id == secretId
                                                                       && s.OwnerId == ownerId
                                                                       && !s.IsBurned,
                                                                    ct);
        if (secret == null)
        {
            return;
        }

        secret.IsBurned = true;
        secret.ExpiresAt = DateTime.UtcNow;

        var quota = await _dbContext.UserQuota.FirstOrDefaultAsync(q => q.UserId == ownerId, ct);
        if (quota != null)
        {
            quota.UsedQuota = Math.Max(0, quota.UsedQuota - secret.Size);
            quota.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> CleanupExpiredSecretsBatchAsync(int batchSize, CancellationToken ct)
    {
        var isInMemory = _dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        if (isInMemory)
        {
            return await ExecuteCleanupExpiredSecretsBatchAsync(batchSize, ct);
        }
        else
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await ExecuteCleanupExpiredSecretsBatchAsync(batchSize, ct);
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
        }
    }

    private async Task<int> ExecuteCleanupExpiredSecretsBatchAsync(int batchSize, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Get the IDs of secrets to delete (only one batch)
        var secretsToDelete = await _dbContext.Secrets
            .Where(s => s.ExpiresAt <= now)
            .Select(s => new { s.Id, s.OwnerId, s.Size })
            .Take(batchSize)
            .ToListAsync(ct);

        if (secretsToDelete.Count == 0)
        {
            return 0;
        }

        // 2. Group by OwnerId and subtract their sizes from the UserQuota
        var quotaUpdates = secretsToDelete
            .GroupBy(s => s.OwnerId)
            .Select(g => new { OwnerId = g.Key, TotalSize = g.Sum(s => s.Size) });

        foreach (var update in quotaUpdates)
        {
            var quota = await _dbContext.UserQuota.FirstOrDefaultAsync(q => q.UserId == update.OwnerId, ct);
            if (quota != null)
            {
                quota.UsedQuota = Math.Max(0, quota.UsedQuota - update.TotalSize);
                quota.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 3. Delete files from disk
        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), StorageFolderName);
        foreach (var s in secretsToDelete)
        {
            var filePath = Path.Combine(storagePath, s.Id.ToString());
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete file {FilePath} from disk during cleanup.", filePath);
                }
            }
        }

        // 4. Delete records from database
        var idsToDelete = secretsToDelete.Select(s => s.Id).ToList();
        int deletedRows;
        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var secrets = await _dbContext.Secrets
                .Where(s => idsToDelete.Contains(s.Id))
                .ToListAsync(ct);
            _dbContext.Secrets.RemoveRange(secrets);
            deletedRows = secrets.Count;
        }
        else
        {
            deletedRows = await _dbContext.Secrets
                .Where(s => idsToDelete.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        return deletedRows;
    }

}