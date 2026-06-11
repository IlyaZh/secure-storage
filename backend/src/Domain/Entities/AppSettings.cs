namespace SecureStorage.Domain.Entities;

public class AppSettings
{
    public string FrontendUrl { get; set; } = string.Empty;
    public string? CookieDomain { get; set; }
    public long MaxSecretSizeBytes { get; set; } = 15728640; // 15 MB default
}
