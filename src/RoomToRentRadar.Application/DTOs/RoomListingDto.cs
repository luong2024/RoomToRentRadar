namespace RoomToRentRadar.Application.DTOs;

/// <summary>
/// Data transfer object representing a room listing on the student browsing screen (AC2).
/// </summary>
public sealed record RoomListingDto(
    string Id,
    string Title,
    decimal MonthlyRent,
    string Currency,
    double AreaSquareMeters,
    double DistanceToCampusKm,
    bool IsVerified,
    string Address,
    string? ThumbnailUrl,
    string CampusId);
