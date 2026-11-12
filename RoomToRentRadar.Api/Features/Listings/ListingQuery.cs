using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingQuery(
    int Limit,
    string? Cursor,
    ListingSort Sort,
    long? MinPrice,
    long? MaxPrice,
    int? Bedrooms,
    double? MinAreaM2,
    double? MaxAreaM2,
    RoomType? RoomType,
    CampusFilter? Campus);

/// <summary>
/// The validated campus radius filter: every listing within
/// <paramref name="MaxDistanceKm"/> kilometres of (Lat, Lng), inclusive.
/// </summary>
public sealed record CampusFilter(double Lat, double Lng, double MaxDistanceKm);
