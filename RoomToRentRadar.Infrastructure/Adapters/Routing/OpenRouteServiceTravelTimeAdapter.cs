using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Routing;

/// <summary>
/// OpenRouteService Directions API v2 adapter (ADR-007, BR-TRV-01..04).
/// Phase 0 stub; full HTTP implementation added in Phase 2.
/// </summary>
public sealed class OpenRouteServiceTravelTimeAdapter : ITravelTimePort
{
    private readonly HttpClient _httpClient;
    private readonly RoutingSettings _settings;

    public OpenRouteServiceTravelTimeAdapter(HttpClient httpClient, RoutingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        _httpClient = httpClient;
        _settings = settings;
    }

    public Task<RoutingModesResult?> GetRoutesAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default) =>
        Task.FromResult<RoutingModesResult?>(null);
}
