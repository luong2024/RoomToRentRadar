using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class ListingQueryParameters
{
    [FromQuery(Name = "limit")]
    [Description("Maximum number of listings to return. Defaults to 20; valid range is 1-100.")]
    public int? Limit { get; init; }

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor from pagination.next_cursor. Must match the requested sort; restart pagination when filters change.")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "sort")]
    [Description("Sort order: newest (default), price_asc, price_desc or area_desc.")]
    public string? Sort { get; init; }

    [FromQuery(Name = "min_price")]
    [Description("Minimum price in whole VND, inclusive. Must be non-negative.")]
    public long? MinPrice { get; init; }

    [FromQuery(Name = "max_price")]
    [Description("Maximum price in whole VND, inclusive. Must be non-negative and at least min_price.")]
    public long? MaxPrice { get; init; }

    [FromQuery(Name = "bedrooms")]
    [Description("Exact number of bedrooms. Valid range is 0-20.")]
    public int? Bedrooms { get; init; }

    [FromQuery(Name = "min_area_m2")]
    [Description("Minimum floor area in square metres, inclusive. Must be finite and non-negative.")]
    public double? MinAreaM2 { get; init; }

    [FromQuery(Name = "max_area_m2")]
    [Description("Maximum floor area in square metres, inclusive. Must be finite, non-negative and at least min_area_m2.")]
    public double? MaxAreaM2 { get; init; }

    [FromQuery(Name = "room_type")]
    [Description("Exact room type: single_room, shared_room, studio, apartment or house.")]
    public string? RoomType { get; init; }

    [FromQuery(Name = "campus_lat")]
    [Description("Latitude of the campus centre in degrees. Requires campus_lng and max_distance_km.")]
    public double? CampusLat { get; init; }

    [FromQuery(Name = "campus_lng")]
    [Description("Longitude of the campus centre in degrees. Requires campus_lat and max_distance_km.")]
    public double? CampusLng { get; init; }

    [FromQuery(Name = "max_distance_km")]
    [Description("Maximum distance to the campus in kilometres, inclusive. Greater than 0 and at most 50.")]
    public double? MaxDistanceKm { get; init; }
}
