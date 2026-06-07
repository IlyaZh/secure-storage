using SecureStorage.Domain.Enums;

namespace SecureStorage.Domain.Entities;

public class Secret
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OwnerId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsOneTime { get; set; } = false;
    public bool IsBurned { get; set; } = false;
    public ContentType ContentType { get; set; } = ContentType.TextPlain;
    public string? FileName { get; set; }
    public byte[] IV { get; set; } = Array.Empty<byte>();
    public long Size { get; set; } = 0;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public User Owner { get; set; } = null!;
}
