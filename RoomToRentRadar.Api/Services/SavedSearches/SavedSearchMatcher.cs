using RoomToRentRadar.Api.Services.Geo;
using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Services.SavedSearches;

/// <summary>
/// Alert rule cốt lõi (US07 / T07.1): một listing có khớp saved search không.
/// Dùng chung cho GET matches và cho rule engine gửi alert (T07.2).
/// </summary>
public static class SavedSearchMatcher
{
    public static bool IsPublic(ListingDocument listing) =>
        string.Equals(listing.Status, "Available", StringComparison.OrdinalIgnoreCase) && listing.IsVerified;

    public static bool Matches(SavedSearchDocument search, ListingDocument listing)
    {
        if (!IsPublic(listing)
            || !string.Equals(search.CampusId, listing.CampusId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (search.MinRent is { } minRent && listing.Rent < minRent) return false;
        if (search.MaxRent is { } maxRent && listing.Rent > maxRent) return false;
        if (search.MinArea is { } minArea && listing.Area < minArea) return false;
        if (search.MaxArea is { } maxArea && listing.Area > maxArea) return false;
        if (search.MaxDistanceMeters is { } maxDistance && listing.DistanceMeters > maxDistance) return false;

        if (!string.IsNullOrWhiteSpace(search.RoomType)
            && !string.Equals(search.RoomType, listing.RoomType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return search.Polygon is not { Count: > 0 } polygon
               || GeoPolygon.Contains(polygon, listing.Location.Coordinates);
    }
}
