namespace SecureStorage.Domain.Entities;

/// <summary>
/// User quota entity
/// </summary>
public class UserQuota
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Quota
    /// </summary>
    public long Quota { get; set; } = 0;

    /// <summary>
    /// Used quota
    /// </summary>
    public long UsedQuota { get; set; } = 0;

    /// <summary>
    /// Created at
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated at
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

}
