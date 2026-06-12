using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;
using SecureStorage.Domain.Settings;


namespace SecureStorage.BackgroundServices;

public class SecretsCleanupWorker(
    IServiceScopeFactory _scopeFactory,
    ILogger<SecretsCleanupWorker> _logger,
    IOptionsMonitor<SecretsCleanupWorkerSettings> _settings
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CleanupExpiredSecretsWorker] started.");
        using var timer = new PeriodicTimer(_settings.CurrentValue.Period);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogInformation("Running cleanup of expired secrets...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var secretService = scope.ServiceProvider.GetRequiredService<ISecretService>();

                    var deletedCount = await secretService.CleanupExpiredSecretsBatchAsync(_settings.CurrentValue.BatchSize, stoppingToken);

                    _logger.LogInformation("[CleanupExpiredSecretsWorker] Deleted {count} secrets in this batch.", deletedCount);

                    if (deletedCount < _settings.CurrentValue.BatchSize)
                    {
                        break;
                    }

                    if (_settings.CurrentValue.BatchDelay.HasValue)
                    {
                        await Task.Delay(_settings.CurrentValue.BatchDelay.Value, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during expired secrets batch cleanup: {Message}", ex.Message);
                    break;
                }
            }
        }

        _logger.LogInformation("[CleanupExpiredSecretsWorker] stopped.");
    }
};
