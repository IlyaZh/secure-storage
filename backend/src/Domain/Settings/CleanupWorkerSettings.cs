namespace SecureStorage.Domain.Settings;

/// <summary>
/// Settings for cleanup worker
/// </summary>
/// <remarks>
/// Period in seconds to run cleanup worker
/// </remarks>
/// <param name="PeriodSeconds">Period in seconds to run cleanup worker</param>
/// <param name="BatchSize">Batch size for cleanup worker</param>
/// <param name="BatchDelayMilliseconds">Delay between batches in milliseconds</param>
/// 
public class CleanupWorkerSettings
{
    public int PeriodSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 1000;
    public int? BatchDelayMilliseconds { get; set; } = 1000;

    public TimeSpan Period => TimeSpan.FromSeconds(PeriodSeconds);
    public TimeSpan? BatchDelay => BatchDelayMilliseconds.HasValue ? TimeSpan.FromMilliseconds(BatchDelayMilliseconds.Value) : null;
}