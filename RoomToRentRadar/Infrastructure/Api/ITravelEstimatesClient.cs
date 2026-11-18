using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Infrastructure.Api;

public enum TravelEstimatesFetchOutcome
{
    Found,
    NotFound
}

public sealed record TravelEstimatesFetchResult(
    TravelEstimatesFetchOutcome Outcome,
    ListingTravelEstimatesDto? Estimates);

public interface ITravelEstimatesClient
{
    Task<TravelEstimatesFetchResult> GetEstimatesAsync(
        Guid listingId,
        double toLat,
        double toLng,
        CancellationToken cancellationToken = default);
}
