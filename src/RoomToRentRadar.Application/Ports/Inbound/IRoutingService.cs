using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IRoutingService
{
    Task<RouteResponse> GetRouteAsync(RouteRequest? request, CancellationToken cancellationToken = default);

    Task<RouteResponse> GetRouteForRoomAsync(
        string roomId,
        double? destinationLatitude,
        double? destinationLongitude,
        CancellationToken cancellationToken = default);
}
