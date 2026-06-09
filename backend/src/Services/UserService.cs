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
    private const long QuotaBytes = 200L * 1024 * 1024;
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

        var isInMemory = _dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        using var transaction = isInMemory ? null : await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
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

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }
            return RegistrationResult.Success;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
    }

    /// <summary>
    /// Get user storage usage statistics (used bytes and quota bytes)
    /// </summary>
    public async Task<UserStorageUsageDto> GetStorageUsageAsync(Guid userId, CancellationToken ct)
    {
        var query = _dbContext.Secrets
            .Where(s => s.OwnerId == userId && !s.IsBurned && s.ExpiresAt > DateTime.UtcNow);

        var usedBytes = await query.AnyAsync(ct)
            ? await query.SumAsync(s => s.Size, ct)
            : 0L;

        return new UserStorageUsageDto(usedBytes, QuotaBytes);
    }
}