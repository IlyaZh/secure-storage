namespace SecureStorage.Domain.Entities;

/// <summary>
/// Settings for cleanup worker
/// </summary>
/// <remarks>
/// Period in seconds to run cleanup worker
/// </remarks>
/// <param name="PeriodSeconds">Period in seconds to run cleanup worker</param>
/// <param name="BatchSize">Batch size for cleanup worker</param>
/// <param name="BatchDelayMilieconds">Delay between batches in milliseconds</param>
public class CleanupWorkerSettings(int PeriodSeconds, int BatchSize = 1000, int? BatchDelayMilieconds = 1000)
{
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(PeriodSeconds);
    public int BatchSize { get; set; } = BatchSize;
    public TimeSpan? BatchDelay { get; set; } = BatchDelayMilieconds.HasValue ? TimeSpan.FromMilliseconds(BatchDelayMilieconds.Value) : null;
};