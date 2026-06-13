using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Enums;

namespace SecureStorage.Domain.Services;

/// <summary>
/// Service for managing users
/// </summary>
public class UserService(
    AppDbContext _dbContext,
    IOptionsSnapshot<AppSettings> _appSettings
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

        var isInMemory = _dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        if (isInMemory)
        {
            return await ExecuteRegistrationLogicAsync(normalizedEmail, inviteCode, ct);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var result = await ExecuteRegistrationLogicAsync(normalizedEmail, inviteCode, ct);
                if (result == RegistrationResult.Success)
                {
                    await transaction.CommitAsync(ct);
                }
                else
                {
                    await transaction.RollbackAsync(ct);
                }
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    private async Task<RegistrationResult> ExecuteRegistrationLogicAsync(string normalizedEmail, Guid inviteCode, CancellationToken ct)
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
        newUser.Quota = new UserQuota
        {
            UserId = newUser.Id,
            Quota = _appSettings.Value.QuotaBytes,
            UsedQuota = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(ct);

        return RegistrationResult.Success;
    }

    /// <summary>
    /// Get user storage usage statistics (used bytes and quota bytes)
    /// </summary>
    public async Task<UserStorageUsageDto> GetStorageUsageAsync(Guid userId, CancellationToken ct)
    {
        var userQuota = await _dbContext.UserQuota.FirstOrDefaultAsync(q => q.UserId == userId, ct);
        
        var usedBytes = userQuota?.UsedQuota ?? 0L;
        var quotaBytes = userQuota != null && userQuota.Quota > 0
            ? userQuota.Quota
            : _appSettings.Value.QuotaBytes;

        return new UserStorageUsageDto(usedBytes, quotaBytes);
    }
}