using System.Text.Json.Serialization;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Lightweight room card DTO for list / similar-rooms responses.
/// Field names and types follow the OpenAPI contract (docs/api/openapi.yaml RoomCardDto schema).
/// Breaking change from legacy field names (Price -> MonthlyRentVnd, Area -> AreaM2, etc.)
/// is intentional per M3 fix — documented in PR description.
/// </summary>
public sealed record RoomCardDto(
    string Id,
    string Title,
    decimal MonthlyRentVnd,
    double AreaM2,
    RoomType RoomType,
    double DistanceKm,
    bool IsVerified,
    ListingStatus Status,
    double Latitude,
    double Longitude,
    string? CoverThumbUrl = null,
    bool IsFavourite = false,
    DateTime? PublishedAtUtc = null
)
{
    // Backward-compat computed properties for internal callers / tests.
    // Marked [JsonIgnore] so they are NEVER serialized in HTTP JSON responses,
    // ensuring strict compliance with OpenAPI contract and payload size (~0.5 KB).
    [JsonIgnore]
    public decimal Price => MonthlyRentVnd;

    [JsonIgnore]
    public double Area => AreaM2;

    [JsonIgnore]
    public double DistanceToCampusKm => DistanceKm;

    [JsonIgnore]
    public bool IsAvailable => Status == ListingStatus.Available;

    [JsonIgnore]
    public string? ThumbnailUrl => CoverThumbUrl;
}
