using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Services.Api;

public enum ListingDetailFetchOutcome
{
    Found,
    NotFound,
    Unavailable
}

public sealed record ListingDetailFetchResult(ListingDetailFetchOutcome Outcome, ListingDetailDto? Detail);
