using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Settings;

namespace SecureStorage.Domain.Services;

public class InvitesService(
    AppDbContext _dbContext,
    IOptionsSnapshot<InvitesCleanupWorkerSettings> _settings
) : IInvitesService
{
    public async Task<Invite> CreateInviteAsync(Guid issuedByUserId, string email, CancellationToken ct)
    {
        var normalizedEmail = email.ToLower().Trim();

        var userExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (userExists)
        {
            throw new InvalidOperationException("User already registered.");
        }

        var activeInviteExists = await _dbContext.Invites.AnyAsync(inv => inv.Email == normalizedEmail && !inv.IsUsed, ct);
        if (activeInviteExists)
        {
            throw new InvalidOperationException("Active invite already exists.");
        }

        var invite = new Invite
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            IsUsed = false,
            IssuedByUserId = issuedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Invites.Add(invite);
        await _dbContext.SaveChangesAsync(ct);
        return invite;
    }

    public async Task<List<Invite>> GetUserInvitesAsync(Guid userId, Guid? lastInviteId, CancellationToken ct)
    {
        var query = _dbContext.Invites
            .Where(inv => inv.IssuedByUserId == userId);

        if (lastInviteId.HasValue)
        {
            query = query.Where(inv => inv.Id < lastInviteId.Value);
        }

        return await query
            .OrderByDescending(inv => inv.Id)
            .Take(20)
            .ToListAsync(ct);
    }

    public async Task<int> CleanupExpiredInvitesBatchAsync(int batchSize, CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.Subtract(_settings.Value.TTL);
        var query = _dbContext.Invites
            .Where(inv => !inv.IsUsed && inv.CreatedAt < threshold);

        var expiredInvites = await query.Take(batchSize).ToListAsync(ct);
        if (expiredInvites.Count == 0) return 0;

        _dbContext.Invites.RemoveRange(expiredInvites);
        await _dbContext.SaveChangesAsync(ct);
        return expiredInvites.Count;
    }
}
