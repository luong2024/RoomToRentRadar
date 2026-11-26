using System.Globalization;

namespace RoomToRentRadar.Models.MapSearch;

public sealed record MapSearchRequest(
    IReadOnlyList<GeoCoordinate> PolygonCoordinates,
    int PageIndex,
    int PageSize);

public sealed record MapSearchRoom(
    string Id,
    string Title,
    decimal MonthlyRent,
    string Currency,
    double AreaSquareMeters,
    string Address,
    double Latitude,
    double Longitude,
    string? ThumbnailUrl)
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public string PriceTag => string.Equals(Currency, "VND", StringComparison.OrdinalIgnoreCase)
        ? $"{(MonthlyRent / 1_000_000m).ToString("0.##", VietnameseCulture)} tr"
        : $"{MonthlyRent:0.##} {Currency}";

    public string PriceDisplay => $"{MonthlyRent:N0} {Currency} / month";
    public string AreaDisplay => $"{AreaSquareMeters:0.#} m²";
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailUrl);
}

public sealed record SavedMapArea(Guid Id, string Name, GeoCoordinate[] Boundary);
