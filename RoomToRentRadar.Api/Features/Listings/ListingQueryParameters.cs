using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class ListingQueryParameters
{
    [FromQuery(Name = "limit")]
    [Description("Maximum number of listings to return. Defaults to 20; valid range is 1-100.")]
    public int? Limit { get; init; }

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor from pagination.next_cursor. Must match the requested sort.")]
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
}
