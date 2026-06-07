using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Enums;

namespace SecureStorage.Domain.Services;

/// <summary>
/// Service for managing users
/// </summary>
public class UserService(
    AppDbContext _dbContext
) : IUserService
{
    /// <summary>
    /// Get user by email
    /// 
    /// Arguments:
    /// - email: The email of the user to get
    /// - ct: The cancellation token
    /// 
    /// Returns:
    /// - The user if found
    /// 
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.ToLower().Trim();
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    /// <summary>
    /// Register user with invite code
    /// 
    /// Arguments:
    /// - email: The email of the user to register
    /// - inviteCode: The invite code to use for registration
    /// - ct: The cancellation token
    /// 
    /// Returns:
    /// - True if the user was registered successfully
    /// - False if the user was not registered successfully
    /// 
    /// </summary>
    public async Task<RegistrationResult> RegisterWithInviteAsync(string email, Guid inviteCode, CancellationToken ct)
    {
        var normalizedEmail = email.ToLower().Trim();

        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
            if (userExists)
                return RegistrationResult.AlreadyRegistered;

            var invite = await _dbContext.Invites.FirstOrDefaultAsync(inv => inv.Id == inviteCode && !inv.IsUsed, ct);
            if (invite == null)
            {
                return RegistrationResult.InviteNotFoundOrUsed;
            }

            if (invite.Email != normalizedEmail)
            {
                return RegistrationResult.EmailMismatch;
            }

            invite.IsUsed = true;
            invite.UsedAt = DateTime.UtcNow;

            var newUser = new User { Id = Guid.CreateVersion7(), Email = normalizedEmail, CreatedAt = DateTime.UtcNow };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return RegistrationResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Create a new invite associated with a user for a specific email
    /// </summary>
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

    /// <summary>
    /// Get invites issued by a specific user with cursor pagination
    /// </summary>
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
}