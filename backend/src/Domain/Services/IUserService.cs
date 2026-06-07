using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Enums;

namespace SecureStorage.Domain.Services;



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
    Task<RegistrationResult> RegisterWithInviteAsync(string email, Guid inviteCode, CancellationToken ct);

    /// <summary>
    /// Create a new invite associated with a user for a specific email
    /// </summary>
    Task<Invite> CreateInviteAsync(Guid issuedByUserId, string email, CancellationToken ct);

    /// <summary>
    /// Get invites issued by a specific user with cursor pagination
    /// </summary>
    Task<List<Invite>> GetUserInvitesAsync(Guid userId, Guid? lastInviteId, CancellationToken ct);

    /// <summary>
    /// Get user storage usage statistics (used bytes and quota bytes)
    /// </summary>
    Task<UserStorageUsageDto> GetStorageUsageAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// DTO for storage usage statistics
/// </summary>
public record UserStorageUsageDto(
    long UsedBytes,
    long QuotaBytes
);