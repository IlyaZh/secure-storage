using System.Net.Mime;

namespace SecureStorage.Services;

public interface ISecretService
{
    Task<Guid> CreateSecretAsync(
        Guid ownerId,
        string? comment,
        bool isOneTime,
        byte[] encryptedData,
        byte[] iv,
        ContentType contentType,
        string? fileName
    );

    Task<SecretDto?> GetSecretAsync(Guid secretId);

    Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId);

    Task BurnSecretAsync(Guid secretId, Guid ownerId);


};

public record SecretDto(
    byte[] EncryptedData,
    byte[] IV,
    ContentType ContentType,
    string? FileName,
    bool IsOneTime
);

public record SecretSummaryDto(
    Guid Id,
    string? Comment,
    bool IsOneTime,
    DateTime CreatedAt
);