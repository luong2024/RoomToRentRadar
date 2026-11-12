namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>RoomCardDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record RoomCardDto(
    string Id,
    string Title,
    long MonthlyRentVnd,
    double AreaM2,
    RoomType RoomType,
    double DistanceKm,
    bool IsVerified,
    ListingStatus Status,
    string? CoverThumbUrl,
    double Latitude,
    double Longitude,
    DateTime? PublishedAtUtc,
    bool IsFavourite
);
