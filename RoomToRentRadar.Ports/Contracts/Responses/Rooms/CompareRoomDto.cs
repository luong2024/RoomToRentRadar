namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>CompareRoomDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record CompareRoomDto(
    string Id,
    string Title,
    string? CoverThumbUrl,
    ListingStatus Status,
    long MonthlyRentVnd,
    long DepositVnd,
    double AreaM2,
    RoomType RoomType,
    double DistanceKm,
    IReadOnlyList<Amenity> Amenities,
    int SafetyScore,
    bool IsSafetyVerified,
    long EstimatedMonthlyTotalVnd
);