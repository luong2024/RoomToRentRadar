using RoomToRentRadar.Ports.Inbound.UseCases.Photos;

namespace RoomToRentRadar.Api.BackgroundJobs;

/// <summary>
/// Hourly cleanup of uploads that were never attached to a listing within 24 h (BR-MED-03).
/// Runs after the first interval — not at startup — so migrations and request handling settle first.
/// Set <c>Media:PhotoCleanupEnabled</c> to <c>false</c> to disable it in tests/local runs.
/// </summary>
public sealed class PhotoCleanupService : BackgroundService
{
    public static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PhotoCleanupService> _logger;

    public PhotoCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PhotoCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Media:PhotoCleanupEnabled", defaultValue: true))
        {
            _logger.LogInformation("Photo cleanup is disabled by configuration (Media:PhotoCleanupEnabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var cleanupUseCase = scope.ServiceProvider.GetRequiredService<ICleanupExpiredPhotosUseCase>();
                var removed = await cleanupUseCase.ExecuteAsync(stoppingToken);

                if (removed > 0)
                {
                    _logger.LogInformation("Deleted {Count} expired unattached photo(s).", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A failed sweep must not kill the background loop; the next tick retries.
                _logger.LogError(exception, "Photo cleanup sweep failed.");
            }
        }
    }
}
