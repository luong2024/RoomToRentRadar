using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Obtains a traffic-network route from an external routing engine.
/// </summary>
public interface IRoutingProvider
{
    Task<RoutingProviderResult?> GetRouteAsync(
        GeoLocation origin,
        GeoLocation destination,
        CancellationToken cancellationToken = default);
}
