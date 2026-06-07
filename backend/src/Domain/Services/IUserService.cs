using SecureStorage.Domain.Entities;

namespace SecureStorage.Domain.Services;

/// <summary>
/// Interface for user service
/// </summary>
public interface IUserService
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
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

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
    Task<bool> RegisterWithInviteAsync(string email, Guid inviteCode, CancellationToken ct);
}