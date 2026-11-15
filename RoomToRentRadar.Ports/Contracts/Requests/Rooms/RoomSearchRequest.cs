namespace RoomToRentRadar.Ports.Contracts.Requests.Rooms;

using RoomToRentRadar.Domain.Enums;

/// <summary>
/// Query parameters of <c>operationId: searchRooms</c> (GET /api/rooms) — US04, BR-SRCH-01..06.
/// Every member is optional; defaults reproduce the "clear filters" state of BR-SRCH-06.
/// </summary>
public sealed record RoomSearchRequest(
    string? CampusId = null,
    long? MinRentVnd = null,
    long? MaxRentVnd = null,
    double? MinAreaM2 = null,
    double? MaxAreaM2 = null,
    IReadOnlyList<RoomType>? RoomTypes = null,
    double? MaxDistanceKm = null,
    string? Sort = null, // null = "distance" (BR-SRCH-04)
    int PageNumber = 1,
    int PageSize = 20)
{
    /// <summary>Whitelist of <c>SortBy</c> values (<c>docs/api/openapi.yaml</c>, BR-SRCH-02).</summary>
    public static IReadOnlyList<string> AllowedSorts { get; } = new[] { "distance", "price_asc", "price_desc", "newest" };

    public const string DefaultSort = "distance";

    public const double MinDistanceKm = 0.1;

    public const double MaxDistanceKmAllowed = 50.0;

    public const int MaxPageSize = 50;
}
