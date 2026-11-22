using RoomToRentRadar.Ports.Models.Travel;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Ports.Outbound.Services;

public interface IRoutingEstimatesProvider
{
    Task<RoutingEstimate?> GetEstimateAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        TravelMode mode,
        CancellationToken cancellationToken);
}
