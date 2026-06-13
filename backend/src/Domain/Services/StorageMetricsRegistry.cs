namespace SecureStorage.Domain.Services;

public class StorageMetrics
{
    public long TotalQuotaBytes { get; set; } = 0;
    public long TotalUsedBytes { get; set; } = 0;
    public long ConfigQuotaBytes { get; set; } = 0;
    public int UserCount { get; set; } = 0;
    public double AverageUsedBytes { get; set; } = 0;
    public double P50 { get; set; } = 0;
    public double P90 { get; set; } = 0;
    public double P95 { get; set; } = 0;
    public double P99 { get; set; } = 0;
}

public class StorageMetricsRegistry
{
    private StorageMetrics _metrics = new();

    public StorageMetrics Metrics
    {
        get => _metrics;
        set => _metrics = value ?? throw new ArgumentNullException(nameof(value));
    }
}
