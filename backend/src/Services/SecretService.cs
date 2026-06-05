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
    private const string StorageFolderName = "Storage";
    private const double SecretTTLhHours = 48;

    /// <summary>
    /// Creates a new secret.
    /// </summary>
    public async Task<Guid> CreateSecretAsync(Stream contentStream,
                                        Guid ownerId,
                                        string? comment,
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

    public Task BurnSecretAsync(Guid secretId, Guid ownerId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<SecretDto?> GetSecretAsync(Guid secretId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

}