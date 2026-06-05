using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;

namespace SecureStorage.Services;

public class UserService(
    AppDbContext _dbContext
) : IUserService
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.ToLower().Trim();
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    public async Task<bool> RegisterWithInviteAsync(string email, Guid inviteCode, CancellationToken ct)
    {
        var normalizedEmail = email.ToLower().Trim();

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

        var newUser = new User { Id = Guid.NewGuid(), Email = normalizedEmail, CreatedAt = DateTime.UtcNow };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(ct);

        return true;
    }
}