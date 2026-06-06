using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Enums;

namespace SecureStorage.Services;

/// <summary>
/// Service for managing secrets.
/// </summary>
public class SecretService(AppDbContext _dbContext) : ISecretService
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
                                        ContentType contentType,
                                        string? fileName,
                                        CancellationToken ct)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == ownerId, ct) ?? throw new UnauthorizedAccessException("The user not found");
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
    /// 
    /// Returns:
    /// - The list of secrets for the user
    /// 
    /// </summary>
    public async Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId, Guid? lastSecretId, CancellationToken ct)
    {
        throw new NotImplementedException();
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

}