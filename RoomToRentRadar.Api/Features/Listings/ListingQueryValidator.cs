using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingQueryValidator
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int MaxBedrooms = 20;
    public const double MaxDistanceKm = 50d;

    public static bool TryCreate(
        ListingQueryParameters parameters,
        out ListingQuery query,
        out ProblemDetails? problem)
    {
        query = null!;
        problem = null;

        var limit = parameters.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidLimit,
                $"limit must be between 1 and {MaxLimit}.");
            return false;
        }

        var sort = ListingSort.Newest;
        if (parameters.Sort is not null && !ListingSortWire.TryParse(parameters.Sort, out sort))
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidSort,
                "sort must be one of: newest, price_asc, price_desc, area_desc.");
            return false;
        }

        if (parameters.MinPrice is < 0 || parameters.MaxPrice is < 0)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidPriceRange,
                "min_price and max_price must be non-negative.");
            return false;
        }

        if (parameters.MinPrice is { } minPrice
            && parameters.MaxPrice is { } maxPrice
            && minPrice > maxPrice)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidPriceRange,
                "min_price cannot be greater than max_price.");
            return false;
        }

        if (!IsValidArea(parameters.MinAreaM2) || !IsValidArea(parameters.MaxAreaM2))
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidAreaRange,
                "min_area_m2 and max_area_m2 must be finite and non-negative.");
            return false;
        }

        if (parameters.MinAreaM2 is { } minAreaM2
            && parameters.MaxAreaM2 is { } maxAreaM2
            && minAreaM2 > maxAreaM2)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidAreaRange,
                "min_area_m2 cannot be greater than max_area_m2.");
            return false;
        }

        if (parameters.Bedrooms is < 0 or > MaxBedrooms)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidBedrooms,
                $"bedrooms must be between 0 and {MaxBedrooms}.");
            return false;
        }

        RoomType? roomType = null;
        if (parameters.RoomType is not null)
        {
            if (!RoomTypeWire.TryParse(parameters.RoomType, out var parsedRoomType))
            {
                problem = ListingProblemFactory.InvalidQuery(
                    ListingProblemFactory.InvalidRoomType,
                    "room_type must be one of: single_room, shared_room, studio, apartment, house.");
                return false;
            }

            roomType = parsedRoomType;
        }

        if (!TryCreateCampusFilter(parameters, out var campus, out problem))
        {
            return false;
        }

        var filters = new ListingFilters(
            parameters.MinPrice,
            parameters.MaxPrice,
            parameters.MinAreaM2,
            parameters.MaxAreaM2,
            parameters.Bedrooms,
            roomType,
            campus);

        query = new ListingQuery(
            limit,
            string.IsNullOrWhiteSpace(parameters.Cursor) ? null : parameters.Cursor,
            sort,
            filters);
        return true;
    }

    private static bool IsValidArea(double? area)
        => area is not { } value || (double.IsFinite(value) && value >= 0d);

    /// <summary>
    /// The campus triple is all-or-nothing: the coordinates alone cannot filter, and
    /// a radius alone has no centre.
    /// </summary>
    private static bool TryCreateCampusFilter(
        ListingQueryParameters parameters,
        out CampusFilter? campus,
        out ProblemDetails? problem)
    {
        campus = null;
        problem = null;

        var hasLat = parameters.CampusLat is not null;
        var hasLng = parameters.CampusLng is not null;
        var hasDistance = parameters.MaxDistanceKm is not null;

        if (!hasLat && !hasLng && !hasDistance)
        {
            return true;
        }

        if (!hasLat || !hasLng || !hasDistance)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidCampusFilter,
                "campus_lat, campus_lng and max_distance_km must be provided together.");
            return false;
        }

        var lat = parameters.CampusLat!.Value;
        var lng = parameters.CampusLng!.Value;
        if (!double.IsFinite(lat) || lat is < -90d or > 90d
            || !double.IsFinite(lng) || lng is < -180d or > 180d)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidCampusCoordinates,
                "campus_lat must be within [-90, 90] and campus_lng within [-180, 180].");
            return false;
        }

        var maxDistanceKm = parameters.MaxDistanceKm!.Value;
        if (!double.IsFinite(maxDistanceKm) || maxDistanceKm <= 0d || maxDistanceKm > MaxDistanceKm)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidDistanceRange,
                "max_distance_km must be greater than 0 and at most "
                + $"{MaxDistanceKm.ToString("0", CultureInfo.InvariantCulture)}.");
            return false;
        }

        campus = new CampusFilter(lat, lng, maxDistanceKm);
        return true;
    }
}
