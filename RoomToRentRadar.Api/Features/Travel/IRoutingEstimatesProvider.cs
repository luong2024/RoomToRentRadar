using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Features.Travel;

public sealed record RoutingEstimate(int DistanceM, int DurationS);

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
