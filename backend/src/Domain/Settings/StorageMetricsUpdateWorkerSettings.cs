using System;

namespace SecureStorage.Domain.Settings;

public class StorageMetricsUpdateWorkerSettings
{
    public int PeriodSeconds { get; set; } = 60;
    public TimeSpan Period => TimeSpan.FromSeconds(PeriodSeconds);
}
