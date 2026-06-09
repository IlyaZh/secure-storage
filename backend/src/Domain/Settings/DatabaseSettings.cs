namespace SecureStorage.Domain.Settings;

public class DatabaseSettings
{
    public DatabaseRetries? Retries { get; set; }
}

public class DatabaseRetries
{
    public int MaxCount { get; set; }
    public int DelayMs { get; set; }
    public TimeSpan Delay => TimeSpan.FromMilliseconds(DelayMs);
}