using SecureStorage.Data;
using SecureStorage.Domain.Enums;

namespace SecureStorage.Services;

public class SecretService(AppDbContext _dbContext) : ISecretService
{
    public Task<Guid> CreateSecretAsync(Guid ownerId,
                                        string? comment,
                                        bool isOneTime,
                                        byte[] encryptedData,
                                        byte[] iv,
                                        ContentType contentType,
                                        string? fileName,
                                        CancellationToken ct)
    {
        throw new NotImplementedException();
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