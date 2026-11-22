using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Services.Api;

public enum TravelEstimatesFetchOutcome
{
    Found,
    NotFound,
    RateLimited
}

public sealed record TravelEstimatesFetchResult(
    TravelEstimatesFetchOutcome Outcome,
    ListingTravelEstimatesDto? Estimates,
    TimeSpan? RetryAfter = null);

public interface ITravelEstimatesClient
{
    Task<TravelEstimatesFetchResult> GetEstimatesAsync(
        Guid listingId,
        string campusId,
        CancellationToken cancellationToken = default);
}
