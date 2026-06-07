using SecureStorage.Domain.Enums;

namespace SecureStorage.Domain.Services;

/// <summary>
/// Interface for secret service
/// </summary>
public interface ISecretService
{
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
    Task<Guid> CreateSecretAsync(Stream contentStream,
                                 Guid ownerId,
                                 string comment,
                                 bool isOneTime,
                                 byte[] iv,
                                 ContentType contentType,
                                 string? fileName,
                                 CancellationToken ct);

    /// <summary>
    /// Get a secret by ID
    /// 
    /// Arguments:
    /// - secretId: The ID of the secret to get
    /// 
    /// Returns:
    /// - The secret if found
    /// 
    /// </summary>
    Task<SecretDto?> GetSecretAsync(Guid secretId, CancellationToken ct);

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
    Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId, Guid? lastSecretId, CancellationToken ct);

    /// <summary>
    /// Burn a secret (mark as used)
    /// 
    /// Arguments:
    /// - secretId: The ID of the secret to burn
    /// - ownerId: The ID of the user who owns the secret
    /// 
    /// </summary>
    Task BurnSecretAsync(Guid secretId, Guid ownerId, CancellationToken ct);

    /// <summary>
    /// Cleanup expired secrets batch
    /// 
    /// Arguments:
    /// - batchSize: The number of secrets to delete in this batch
    /// - ct: The cancellation token
    /// 
    /// Returns:
    /// - The number of secrets cleaned up in this batch
    /// 
    /// </summary>
    Task<int> CleanupExpiredSecretsBatchAsync(int batchSize, CancellationToken ct);
};

/// <summary>
/// DTO for a secret
/// </summary>
public record SecretDto(
    byte[] EncryptedData,
    byte[] IV,
    ContentType ContentType,
    string? FileName,
    bool IsOneTime
);

/// <summary>
/// DTO for a secret summary
/// </summary>
public record SecretSummaryDto(
    Guid Id,
    string? Comment,
    bool IsOneTime,
    DateTime CreatedAt
);