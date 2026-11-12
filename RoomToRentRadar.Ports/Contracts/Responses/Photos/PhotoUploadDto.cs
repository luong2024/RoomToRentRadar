using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

public sealed record PhotoUploadDto(
    string Id,
    PhotoKind Kind,
    string ThumbUrl,
    string MediumUrl,
    int Width,
    int Height,
    long SizeBytes);
