using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Services.Api;

public enum ListingPhotosUploadOutcome
{
    Uploaded,
    Unauthorized,
    Forbidden,
    ListingNotFound,
    ListingUnavailable,
    InvalidPhoto,
    PhotoLimitExceeded,
    PhotoTooLarge,
    UnsupportedType,
    Failed
}

public sealed record ListingPhotosUploadResult(
    ListingPhotosUploadOutcome Outcome,
    IReadOnlyList<ListingPhotoDto> Photos);
