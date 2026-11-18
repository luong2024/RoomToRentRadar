namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>PhotoUploadDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record PhotoUploadDto(
    string Id,
    PhotoKind Kind,
    string ThumbUrl,
    string MediumUrl,
    int Width,
    int Height,
    long SizeBytes
);