using System.Globalization;

namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Search and filter criteria matching the backend SearchRoomsQuery contract.
/// </summary>
public sealed record RoomSearchCriteria
{
    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public double? MinArea { get; init; }

    public double? MaxArea { get; init; }

    public string? RoomType { get; init; }

    public string? Keyword { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? RadiusInKm { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public bool HasActiveFilters =>
        MinPrice.HasValue ||
        MaxPrice.HasValue ||
        MinArea.HasValue ||
        MaxArea.HasValue ||
        !string.IsNullOrWhiteSpace(RoomType) ||
        RadiusInKm.HasValue ||
        !string.IsNullOrWhiteSpace(Keyword);

    public string ToQueryString()
    {
        var parameters = new List<string>(10);

        if (MinPrice.HasValue)
        {
            parameters.Add($"minPrice={MinPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (MaxPrice.HasValue)
        {
            parameters.Add($"maxPrice={MaxPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (MinArea.HasValue)
        {
            parameters.Add($"minArea={MinArea.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (MaxArea.HasValue)
        {
            parameters.Add($"maxArea={MaxArea.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(RoomType))
        {
            parameters.Add($"roomType={Uri.EscapeDataString(RoomType.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            parameters.Add($"keyword={Uri.EscapeDataString(Keyword.Trim())}");
        }

        if (Latitude.HasValue)
        {
            parameters.Add($"latitude={Latitude.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Longitude.HasValue)
        {
            parameters.Add($"longitude={Longitude.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (RadiusInKm.HasValue)
        {
            parameters.Add($"radiusInKm={RadiusInKm.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        parameters.Add($"pageNumber={Math.Max(1, PageNumber)}");
        parameters.Add($"pageSize={Math.Max(1, PageSize)}");

        return string.Join("&", parameters);
    }
}
