namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class RoomProjectionReconciliationService : BackgroundService
{
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RoomProjectionReconciliationService> _logger;

    public RoomProjectionReconciliationService(
        IServiceScopeFactory scopeFactory,
        ILogger<RoomProjectionReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<CosmosRoomListingRepository>();
                await repository.ReconcilePendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Room projection reconciliation pass failed; pending intents will be retried.");
            }

            try
            {
                await Task.Delay(ReconciliationInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
