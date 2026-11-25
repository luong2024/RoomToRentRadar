namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

/// <summary>Distance and duration for a single routed travel profile.</summary>
public sealed record RouteLegResult(int DistanceMeters, int DurationSeconds);

/// <summary>Routed driving and walking legs between a room and a campus.</summary>
public sealed record RoutingModesResult(RouteLegResult Driving, RouteLegResult Walking);

/// <summary>
/// Outbound routing service port (ADR-007, BR-TRV-01..04).
/// Implementations enforce a 3-second timeout and return <c>null</c> when routing is unavailable.
/// </summary>
public interface ITravelTimePort
{
    Task<RoutingModesResult?> GetRoutesAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default);
}
