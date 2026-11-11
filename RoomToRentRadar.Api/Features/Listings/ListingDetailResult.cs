using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public enum ListingDetailOutcome
{
    Found,
    NotFound,
    Unavailable
}

public sealed record ListingDetailResult(ListingDetailOutcome Outcome, ListingDetailDto? Detail);
