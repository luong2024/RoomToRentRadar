using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;

namespace RoomToRentRadar.Api.BackgroundJobs;

// BR-MED-03 / system-architecture §6.4: hourly cleanup of unattached photos older than 24 h.
public sealed class PhotoCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotoCleanupService> _logger;

    public PhotoCleanupService(IServiceScopeFactory scopeFactory, ILogger<PhotoCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCleanupAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task RunCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ICleanupExpiredPhotosUseCase>();
            var deleted = await useCase.ExecuteAsync(stoppingToken);

            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} expired unattached photos.", deleted);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Photo cleanup failed.");
        }
    }
}
