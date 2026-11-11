namespace RoomToRentRadar.Ports.Contracts.Requests.Rooms;

public sealed record RoomSearchRequest(
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    double? MinArea = null,
    double? MaxArea = null,
    double? Latitude = null,
    double? Longitude = null,
    double? MaxDistanceKm = null,
    string? Keyword = null,
    string? SortBy = null, // "distance", "price_asc", "price_desc", "area_asc", "area_desc", "newest"
    int Page = 1,
    int PageSize = 10
);
