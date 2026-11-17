namespace RoomToRentRadar.Application.DTOs;

public record SearchRoomsQuery
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

    public SearchRoomsQuery()
    {
    }

    public SearchRoomsQuery(
        decimal? minPrice = null,
        decimal? maxPrice = null,
        double? minArea = null,
        double? maxArea = null,
        string? roomType = null,
        string? keyword = null,
        double? latitude = null,
        double? longitude = null,
        double? radiusInKm = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        MinArea = minArea;
        MaxArea = maxArea;
        RoomType = roomType;
        Keyword = keyword;
        Latitude = latitude;
        Longitude = longitude;
        RadiusInKm = radiusInKm;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
