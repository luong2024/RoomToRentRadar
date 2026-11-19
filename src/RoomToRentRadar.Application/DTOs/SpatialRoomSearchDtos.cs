namespace RoomToRentRadar.Application.DTOs;

/// <summary>
/// Geographic coordinate pair (Latitude, Longitude).
/// </summary>
public sealed record CoordinateDto(double Latitude, double Longitude);

/// <summary>
/// Request payload for searching rooms within a drawn map polygon.
/// </summary>
public sealed record SpatialRoomSearchRequestDto
{
    /// <summary>
    /// Sequence of boundary coordinates representing the drawn polygon.
    /// Minimum 3 distinct points required.
    /// </summary>
    public List<CoordinateDto> PolygonCoordinates { get; init; } = [];

    /// <summary>
    /// Minimum monthly rent amount filter.
    /// </summary>
    public decimal? MinPrice { get; init; }

    /// <summary>
    /// Maximum monthly rent amount filter.
    /// </summary>
    public decimal? MaxPrice { get; init; }

    /// <summary>
    /// Room type filter (e.g. Single, Studio, Apartment).
    /// </summary>
    public string? RoomType { get; init; }

    /// <summary>
    /// Required amenities filter (e.g. AirConditioning, WaterHeater).
    /// </summary>
    public List<string>? Amenities { get; init; }

    /// <summary>
    /// Sort field (e.g. "price", "area", "created"). Defaults to database order.
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// Sort descending flag.
    /// </summary>
    public bool Descending { get; init; }

    /// <summary>
    /// 1-based page index.
    /// </summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>
    /// Number of items per page (1 to 50).
    /// </summary>
    public int PageSize { get; init; } = 10;
}

/// <summary>
/// Filter criteria passed to the repository for spatial querying.
/// </summary>
public sealed record RoomSearchFilter(
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? RoomType = null,
    IReadOnlyList<string>? Amenities = null,
    string? SortBy = null,
    bool Descending = false);

/// <summary>
/// Room summary result dto returned by spatial polygon search.
/// Includes exact coordinates for map pin visualization (T05.3).
/// </summary>
public sealed record SpatialRoomSearchResultDto(
    string Id,
    string Title,
    decimal MonthlyRent,
    string Currency,
    double AreaSquareMeters,
    string Address,
    double Latitude,
    double Longitude,
    bool IsVerified,
    string? ThumbnailUrl,
    string CampusId);
