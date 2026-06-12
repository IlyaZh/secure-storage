using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;
using SecureStorage.Domain.Settings;


namespace SecureStorage.BackgroundServices;

public class InvitesCleanupWorker(
    IServiceScopeFactory _scopeFactory,
    ILogger<InvitesCleanupWorker> _logger,
    IOptionsMonitor<InvitesCleanupWorkerSettings> _settings
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CleanupExpiredInvitesWorker] started.");
        using var timer = new PeriodicTimer(_settings.CurrentValue.Period);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogInformation("Running cleanup of expired Invites...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var Inviteservice = scope.ServiceProvider.GetRequiredService<IInvitesService>();

                    var deletedCount = await Inviteservice.CleanupExpiredInvitesBatchAsync(_settings.CurrentValue.BatchSize, stoppingToken);

                    _logger.LogInformation("[CleanupExpiredInvitesWorker] Deleted {count} Invites in this batch.", deletedCount);

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
                    _logger.LogError(ex, "Error occurred during expired Invites batch cleanup: {Message}", ex.Message);
                    break;
                }
            }
        }

        _logger.LogInformation("[CleanupExpiredInvitesWorker] stopped.");
    }
};
