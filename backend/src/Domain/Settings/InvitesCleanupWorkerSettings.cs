namespace SecureStorage.Domain.Settings;

/// <summary>
/// Settings for cleanup worker
/// </summary>
/// <param name="PeriodSeconds">Period in seconds to run cleanup worker</param>
/// <param name="BatchSize">Batch size for cleanup worker</param>
/// <param name="BatchDelayMilliseconds">Delay between batches in milliseconds</param>
/// <param name="TTL">TTL for invites</param>
/// 
public class InvitesCleanupWorkerSettings
{
    public int PeriodSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 1000;
    public int? BatchDelayMilliseconds { get; set; } = 1000;
    public int TTLh { get; set; } = 120;

    public TimeSpan Period => TimeSpan.FromSeconds(PeriodSeconds);
    public TimeSpan? BatchDelay => BatchDelayMilliseconds.HasValue ? TimeSpan.FromMilliseconds(BatchDelayMilliseconds.Value) : null;
    public TimeSpan TTL => TimeSpan.FromHours(TTLh);
}