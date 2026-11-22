using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.SavedSearches;

namespace RoomToRentRadar.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs the alert sweep immediately at startup and then on a fixed interval.
/// With a five-minute interval, a listing published just after a sweep is still
/// alerted well within the fifteen-minute product requirement.
/// </summary>
public sealed class SavedSearchAlertWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AlertsOptions> options,
    ILogger<SavedSearchAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Saved search alert worker is disabled.");
            return;
        }

        await SweepAsync(stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.SweepIntervalMinutes));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var evaluator = scope.ServiceProvider.GetRequiredService<ISavedSearchAlertUseCase>();
            var created = await evaluator.SweepAsync(DateTime.UtcNow, cancellationToken);
            if (created > 0)
            {
                logger.LogInformation("Saved search alert sweep delivered {Count} notification(s).", created);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Saved search alert sweep failed.");
        }
    }
}
