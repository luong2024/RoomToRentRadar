using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

/// <summary>
/// Development-only deterministic routing mock (ADR-007, GUARDRAILS §1.5.4).
/// Phase 0 stub; full deterministic estimation added in Phase 2.
/// </summary>
public sealed class MockTravelTimeAdapter : ITravelTimePort
{
    public Task<RoutingModesResult?> GetRoutesAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default) =>
        Task.FromResult<RoutingModesResult?>(null);
}
