using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Infrastructure.Api;

public enum ListingDetailFetchOutcome
{
    Found,
    NotFound,
    Unavailable
}

public sealed record ListingDetailFetchResult(ListingDetailFetchOutcome Outcome, ListingDetailDto? Detail);
