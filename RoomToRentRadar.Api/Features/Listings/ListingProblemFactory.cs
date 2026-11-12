using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingProblemFactory
{
    public const string InvalidLimit = "invalid_limit";
    public const string InvalidSort = "invalid_sort";
    public const string InvalidCursor = "invalid_cursor";
    public const string CursorSortMismatch = "cursor_sort_mismatch";
    public const string InvalidPriceRange = "invalid_price_range";
    public const string InvalidAreaRange = "invalid_area_range";
    public const string InvalidRoomType = "invalid_room_type";
    public const string InvalidCampusFilter = "invalid_campus_filter";
    public const string InvalidCampusCoordinates = "invalid_campus_coordinates";
    public const string InvalidDistanceRange = "invalid_distance_range";
    public const string InvalidBedrooms = "invalid_bedrooms";
    public const string InvalidParameterFormat = "invalid_parameter_format";
    public const string ListingNotFound = "listing_not_found";

    public static ProblemDetails InvalidQuery(string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid listings query",
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    public static ProblemDetails NotFound()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            Status = StatusCodes.Status404NotFound,
            Title = "Listing not found",
            Detail = "The listing does not exist or is not published."
        };
        problem.Extensions["code"] = ListingNotFound;
        return problem;
    }
}
