using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;

namespace SecureStorage.Domain.Services;

/// <summary>
/// Service for managing secrets.
/// </summary>
public class SecretService(AppDbContext _dbContext, ILogger<SecretService> _logger) : ISecretService
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
        var now = DateTime.UtcNow;
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
            CreatedAt = now,
            ExpiresAt = now.AddHours(SecretTTLhHours)
        };

        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), StorageFolderName);
        if (!Directory.Exists(storagePath))
        {
            Directory.CreateDirectory(storagePath);
        }

        var filePath = Path.Combine(storagePath, secretId.ToString());
        using (var fileStream = File.Create(filePath))
        {
            await contentStream.CopyToAsync(fileStream, ct);
        }
        secret.Size = new FileInfo(filePath).Length;

        _dbContext.Secrets.Add(secret);
        await _dbContext.SaveChangesAsync(ct);

        return secretId;
    }

    /// <summary>
    /// Get a secret by ID
    /// 
    /// Arguments:
    /// - secretId: The ID of the secret to get
    /// - ownerId: The ID of the user who owns the secret
    /// 
    /// Returns:
    /// - The secret if found
    /// 
    /// </summary>
    public async Task<SecretDto?> GetSecretAsync(Guid secretId, CancellationToken ct)
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

        if (secret.IsOneTime)
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
        var secret = await _dbContext.Secrets.FirstOrDefaultAsync(s => s.Id == secretId
                                                                       && s.OwnerId == ownerId,
                                                                    ct);
        if (secret == null)
        {
            return;
        }
        secret.IsBurned = true;
        secret.ExpiresAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> CleanupExpiredSecretsBatchAsync(int batchSize, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Get the IDs of secrets to delete (only one batch)
        var secretsToDelete = await _dbContext.Secrets
            .Where(s => s.ExpiresAt <= now)
            .Select(s => s.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        if (secretsToDelete.Count == 0)
        {
            return 0;
        }

        // 2. Delete files from disk
        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), StorageFolderName);
        foreach (var id in secretsToDelete)
        {
            var filePath = Path.Combine(storagePath, id.ToString());
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

        // 3. Delete records from database
        int deletedRows;
        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var secrets = await _dbContext.Secrets
                .Where(s => secretsToDelete.Contains(s.Id))
                .ToListAsync(ct);
            _dbContext.Secrets.RemoveRange(secrets);
            deletedRows = await _dbContext.SaveChangesAsync(ct);
        }
        else
        {
            deletedRows = await _dbContext.Secrets
                .Where(s => secretsToDelete.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
        }

        return deletedRows;
    }

}