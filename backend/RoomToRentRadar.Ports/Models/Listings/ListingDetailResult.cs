using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Listings;

public enum ListingDetailOutcome
{
    Found,
    NotFound,
    Unavailable
}

public sealed record ListingDetailResult(ListingDetailOutcome Outcome, ListingDetailDto? Detail);
