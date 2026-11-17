using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

/// <summary>A photo attached to a listing (openapi <c>PhotoDto</c>, BR-MED-05: sortOrder 0 = cover).</summary>
public sealed record PhotoDto(
    string Id,
    PhotoKind Kind,
    string ThumbUrl,
    string MediumUrl,
    string OriginalUrl,
    int Width,
    int Height,
    int SortOrder);
