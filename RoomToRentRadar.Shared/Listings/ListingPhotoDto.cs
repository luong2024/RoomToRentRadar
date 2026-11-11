namespace RoomToRentRadar.Shared.Listings;

public sealed record ListingPhotoDto(
    Guid Id,
    Guid ListingId,
    string Url,
    string? ThumbUrl,
    int Width,
    int Height,
    ListingPhotoKind Kind,
    string? Alt,
    int SortOrder,
    bool IsCover);
