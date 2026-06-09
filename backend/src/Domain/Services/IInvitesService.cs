using SecureStorage.Domain.Entities;

namespace SecureStorage.Domain.Services;

public interface IInvitesService
{
    Task<Invite> CreateInviteAsync(Guid issuedByUserId, string email, CancellationToken ct);
    Task<List<Invite>> GetUserInvitesAsync(Guid userId, Guid? lastInviteId, CancellationToken ct);
    Task<int> CleanupExpiredInvitesBatchAsync(int batchSize, CancellationToken ct);
}
