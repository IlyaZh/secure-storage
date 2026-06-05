namespace SecureStorage.Domain.Entities;

/// <summary>
/// Represents an invite to the system.
/// </summary>
public class Invite
{
    /// <summary>
    /// Gets or sets the unique identifier of the invite.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the email address of the invitee.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets whether the invite has been used.
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Gets or sets the identifier of the user who issued the invite.
    /// </summary>
    public Guid IssuedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the invite was used.
    /// </summary>
    public DateTime? UsedAt { get; set; } = null;

    /// <summary>
    /// Gets or sets the date and time when the invite was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
