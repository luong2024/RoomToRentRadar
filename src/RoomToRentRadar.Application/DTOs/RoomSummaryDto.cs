namespace RoomToRentRadar.Application.DTOs;

public record RoomSummaryDto(
    string Id,
    string Title,
    decimal Price,
    double Area,
    string RoomType,
    bool IsVerified,
    double? DistanceInKm,
    string Address = "",
    string? ThumbnailUrl = null,
    IReadOnlyList<string>? Amenities = null,
    DateTimeOffset? CreatedAt = null
);
