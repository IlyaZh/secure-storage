using SecureStorage.Domain.Entities;

namespace SecureStorage.Services;

/// <summary>
/// Interface for user service
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Get user by email
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Register user with invite code
    /// </summary>
    Task<bool> RegisterWithInviteAsync(string email, Guid inviteCode, CancellationToken ct);
}