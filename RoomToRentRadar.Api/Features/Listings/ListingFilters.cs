using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// The validated filters of a listing search, independent of paging and sort.
/// </summary>
public sealed record ListingFilters(
    long? MinPrice = null,
    long? MaxPrice = null,
    double? MinAreaM2 = null,
    double? MaxAreaM2 = null,
    int? Bedrooms = null,
    RoomType? RoomType = null,
    CampusFilter? Campus = null)
{
    public bool IsEmpty => MinPrice is null
        && MaxPrice is null
        && MinAreaM2 is null
        && MaxAreaM2 is null
        && Bedrooms is null
        && RoomType is null
        && Campus is null;
}

/// <summary>
/// The validated campus radius filter: every listing within
/// <paramref name="MaxDistanceKm"/> kilometres of (Lat, Lng), inclusive.
/// </summary>
public sealed record CampusFilter(double Lat, double Lng, double MaxDistanceKm);
