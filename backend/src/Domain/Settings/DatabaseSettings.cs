namespace SecureStorage.Domain.Settings;

public class DatabaseSettings
{
    public DatabaseRetries? Retries { get; set; }
}

public class DatabaseRetries(int MaxCount, int DelayMs)
{
    public int MaxCount { get; set; } = MaxCount;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(DelayMs);

}