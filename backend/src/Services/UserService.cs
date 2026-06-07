using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;

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
    public async Task<bool> RegisterWithInviteAsync(string email, Guid inviteCode, CancellationToken ct)
    {
        var normalizedEmail = email.ToLower().Trim();

        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
            if (userExists)
                return false;

            var invite = await _dbContext.Invites.FirstOrDefaultAsync(inv => inv.Id == inviteCode && !inv.IsUsed, ct);
            if (invite == null)
            {
                return false;
            }

            invite.IsUsed = true;
            invite.Email = normalizedEmail;

            var newUser = new User { Id = Guid.CreateVersion7(), Email = normalizedEmail, CreatedAt = DateTime.UtcNow };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}