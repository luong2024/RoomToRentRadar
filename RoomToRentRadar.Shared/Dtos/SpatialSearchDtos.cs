namespace RoomToRentRadar.Shared.Dtos;

/// <summary>
/// Body cho POST /api/v1/listings/spatial-search (US05 / T05.2).
/// Polygon là GeoJSON ring: mỗi điểm [longitude, latitude], điểm cuối trùng điểm đầu.
/// </summary>
public sealed class SpatialSearchRequestDto
{
    public List<double[]> Polygon { get; set; } = [];

    /// <summary>Xem <see cref="ListingSortKeys"/> (mặc định distance).</summary>
    public string SortBy { get; set; } = ListingSortKeys.Default;

    public string CampusId { get; set; } = CampusIds.Default;
}

public sealed class SpatialSearchPinDto
{
    public string Id { get; set; } = string.Empty;
    public string CampusId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public double Area { get; set; }
    public double DistanceMeters { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class SpatialSearchResponseDto
{
    public int TotalCount { get; set; }
    public List<SpatialSearchPinDto> Items { get; set; } = [];
}
