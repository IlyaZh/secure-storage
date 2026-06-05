using SecureStorage.Domain.Enums;

namespace SecureStorage.Services;

/// <summary>
/// Interface for secret service
/// </summary>
public interface ISecretService
{
    /// <summary>
    /// Create a new secret
    /// </summary>
    Task<Guid> CreateSecretAsync(Stream contentStream,
                                 Guid ownerId,
                                 string? comment,
                                 bool isOneTime,
                                 byte[] iv,
                                 ContentType contentType,
                                 string? fileName,
                                 CancellationToken ct);

    /// <summary>
    /// Get a secret by ID
    /// </summary>
    Task<SecretDto?> GetSecretAsync(Guid secretId, CancellationToken ct);

    /// <summary>
    /// Get all secrets for a user
    /// </summary>
    Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId, CancellationToken ct);

    /// <summary>
    /// Burn a secret (mark as used)
    /// </summary>
    Task BurnSecretAsync(Guid secretId, Guid ownerId, CancellationToken ct);


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