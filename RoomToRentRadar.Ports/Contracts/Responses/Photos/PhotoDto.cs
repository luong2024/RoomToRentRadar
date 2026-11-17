namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>PhotoDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record PhotoDto(
    string Id,
    PhotoKind Kind,
    string ThumbUrl,
    string MediumUrl,
    string OriginalUrl,
    int Width,
    int Height,
    int SortOrder
);