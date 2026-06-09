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