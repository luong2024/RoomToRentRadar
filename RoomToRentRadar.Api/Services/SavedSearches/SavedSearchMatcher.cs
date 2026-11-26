using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Geo;
using RoomToRentRadar.Api.Services.Mapping;

namespace RoomToRentRadar.Api.Services.SavedSearches;

/// <summary>
/// Alert rule cốt lõi (US07 / T07.1): một listing có khớp saved search không.
/// Dùng chung cho GET matches và cho rule engine gửi alert (T07.2).
/// </summary>
public static class SavedSearchMatcher
{
    public static bool Matches(SavedSearchDocument search, ListingDocument listing) =>
        listing.IsPublic()
        && string.Equals(search.CampusId, listing.CampusId, StringComparison.OrdinalIgnoreCase)
        && MatchesFilters(search, listing)
        && MatchesPolygon(search, listing);

    private static bool MatchesFilters(SavedSearchDocument search, ListingDocument listing) =>
        (search.MinRent is not { } minRent || listing.Rent >= minRent)
        && (search.MaxRent is not { } maxRent || listing.Rent <= maxRent)
        && (search.MinArea is not { } minArea || listing.Area >= minArea)
        && (search.MaxArea is not { } maxArea || listing.Area <= maxArea)
        && (search.MaxDistanceMeters is not { } maxDistance || listing.DistanceMeters <= maxDistance)
        && (string.IsNullOrWhiteSpace(search.RoomType)
            || string.Equals(search.RoomType, listing.RoomType, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesPolygon(SavedSearchDocument search, ListingDocument listing) =>
        search.Polygon is not { Count: > 0 } polygon
        || GeoPolygon.Contains(polygon, listing.Location.Coordinates);
}
