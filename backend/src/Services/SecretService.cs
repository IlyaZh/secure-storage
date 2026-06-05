namespace SecureStorage.Services;

public class SecretService : ISecretService
{
    public Task<Guid> CreateSecretAsync(Guid ownerId, string? comment, bool isOneTime, byte[] encryptedData, byte[] iv, ContentType contentType, string? fileName)
    {
        throw new NotImplementedException();
    }

    public Task BurnSecretAsync(Guid secretId, Guid ownerId)
    {
        throw new NotImplementedException();
    }

    public Task<SecretDto?> GetSecretAsync(Guid secretId)
    {
        throw new NotImplementedException();
    }

    public Task<List<SecretSummaryDto>> GetUserSecretsAsync(Guid ownerId)
    {
        throw new NotImplementedException();
    }
}