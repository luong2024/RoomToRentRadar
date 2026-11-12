using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

public sealed record PhotoDto(
    string Id,
    PhotoKind Kind,
    string ThumbUrl,
    string MediumUrl,
    string OriginalUrl,
    int Width,
    int Height,
    int SortOrder);
