using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

/// <summary>Response of <c>operationId: uploadPhoto</c> (openapi <c>PhotoUploadDto</c>, US08).</summary>
public sealed record PhotoUploadDto(
    string Id,
    PhotoKind Kind,
    string ThumbUrl,
    string MediumUrl,
    int Width,
    int Height,
    long SizeBytes);
