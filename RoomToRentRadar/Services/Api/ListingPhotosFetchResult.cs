using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Services.Api;

public enum ListingPhotosFetchOutcome
{
    Found,
    NotFound,
    Unavailable
}

public sealed record ListingPhotosFetchResult(
    ListingPhotosFetchOutcome Outcome,
    IReadOnlyList<ListingPhotoDto> Photos);
