using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.Listings;

/// <summary>
/// Body of <c>POST /api/v1/listings/search/area</c>: a drawn polygon plus the
/// listing filters, sort and paging options supported by the list endpoint.
/// </summary>
public sealed class AreaSearchRequest
{
    [Required]
    [Description("Drawn search area. Only a GeoJSON Polygon with one closed exterior ring is supported.")]
    public AreaPolygon? Area { get; init; }

    [Description("Optional price and bedroom filters, identical to the list endpoint query parameters.")]
    public AreaSearchFilters? Filters { get; init; }

    [Description("Sort order: newest (default), price_asc, price_desc or area_desc.")]
    public string? Sort { get; init; }

    [Description("Maximum number of listings to return. Defaults to 20; valid range is 1-100.")]
    public int? Limit { get; init; }

    [Description("Opaque cursor from pagination.next_cursor. Must match the requested sort.")]
    public string? Cursor { get; init; }
}

/// <summary>
/// GeoJSON polygon in WGS84. Every position is <c>[lng, lat]</c>; only the
/// exterior ring is accepted and it must be closed.
/// </summary>
public sealed class AreaPolygon
{
    [Required]
    [Description("GeoJSON geometry type. The only supported value is \"Polygon\".")]
    public string? Type { get; init; }

    [Required]
    [Description("Polygon rings. Exactly one closed exterior ring of [lng, lat] positions.")]
    public double[][][]? Coordinates { get; init; }
}

public sealed class AreaSearchFilters
{
    [Description("Minimum price in whole VND, inclusive. Must be non-negative.")]
    public long? MinPrice { get; init; }

    [Description("Maximum price in whole VND, inclusive. Must be non-negative and at least minPrice.")]
    public long? MaxPrice { get; init; }

    [Description("Exact number of bedrooms. Valid range is 0-20.")]
    public int? Bedrooms { get; init; }
}
