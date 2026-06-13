using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;
using SecureStorage.Domain.Settings;

namespace SecureStorage.BackgroundServices;

public class StorageMetricsUpdateWorker(
    IServiceScopeFactory _scopeFactory,
    ILogger<StorageMetricsUpdateWorker> _logger,
    IOptionsMonitor<StorageMetricsUpdateWorkerSettings> _settings,
    StorageMetricsRegistry _metricsRegistry
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[StorageMetricsUpdateWorker] started.");

        // Initial run on startup
        await UpdateMetricsAsync(stoppingToken);

        using var timer = new PeriodicTimer(_settings.CurrentValue.Period);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await UpdateMetricsAsync(stoppingToken);
        }

        _logger.LogInformation("[StorageMetricsUpdateWorker] stopped.");
    }

    private async Task UpdateMetricsAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Updating in-memory storage metrics...");
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var appSettings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>();

            var userQuotas = await dbContext.UserQuota
                .Select(q => new { q.Quota, q.UsedQuota })
                .ToListAsync(ct);

            long totalQuotaBytes = userQuotas.Count > 0 ? userQuotas.Sum(q => q.Quota) : 0;
            long totalUsedBytes = userQuotas.Count > 0 ? userQuotas.Sum(q => q.UsedQuota) : 0;
            long configQuotaBytes = appSettings.Value.QuotaBytes;

            var freeQuotaPercentages = userQuotas
                .Select(q => q.Quota > 0 ? ((q.Quota - q.UsedQuota) * 100.0 / q.Quota) : 0.0)
                .OrderBy(p => p)
                .ToList();

            var newMetrics = new StorageMetrics
            {
                TotalQuotaBytes = totalQuotaBytes,
                TotalUsedBytes = totalUsedBytes,
                ConfigQuotaBytes = configQuotaBytes,
                UserCount = userQuotas.Count,
                AverageUsedBytes = userQuotas.Count > 0 ? Math.Round(userQuotas.Average(q => q.UsedQuota), 2) : 0.0,
                P50 = GetPercentile(freeQuotaPercentages, 50),
                P90 = GetPercentile(freeQuotaPercentages, 90),
                P95 = GetPercentile(freeQuotaPercentages, 95),
                P99 = GetPercentile(freeQuotaPercentages, 99),
            };

            _metricsRegistry.Metrics = newMetrics;
            _logger.LogInformation("Storage metrics successfully updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating storage metrics: {Message}", ex.Message);
        }
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues == null || sortedValues.Count == 0) return 0.0;
        if (sortedValues.Count == 1) return Math.Round(sortedValues[0], 2);

        double realIndex = (percentile / 100.0) * (sortedValues.Count - 1);
        int index = (int)realIndex;
        double frac = realIndex - index;
        if (index >= sortedValues.Count - 1) return Math.Round(sortedValues[^1], 2);
        return Math.Round(sortedValues[index] * (1.0 - frac) + sortedValues[index + 1] * frac, 2);
    }
}
