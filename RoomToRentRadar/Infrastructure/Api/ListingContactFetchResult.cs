using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Infrastructure.Api;

public enum ListingContactFetchOutcome
{
    Found,
    Unauthorized,
    NotFound,
    Unavailable
}

public sealed record ListingContactFetchResult(
    ListingContactFetchOutcome Outcome,
    ListingContactDto? Contact);
