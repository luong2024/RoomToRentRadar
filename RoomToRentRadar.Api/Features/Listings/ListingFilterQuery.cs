using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Applies the SQL-translatable filters. Location filters are evaluated by the
/// caller because the radius search narrows candidates with a bounding box first.
/// </summary>
public static class ListingFilterQuery
{
    public static IQueryable<Listing> ApplyFilters(
        this IQueryable<Listing> listings,
        ListingFilters filters)
    {
        if (filters.MinPrice is { } minPrice)
        {
            listings = listings.Where(x => x.Price >= minPrice);
        }

        if (filters.MaxPrice is { } maxPrice)
        {
            listings = listings.Where(x => x.Price <= maxPrice);
        }

        if (filters.MinAreaM2 is { } minAreaM2)
        {
            listings = listings.Where(x => x.AreaM2 >= minAreaM2);
        }

        if (filters.MaxAreaM2 is { } maxAreaM2)
        {
            listings = listings.Where(x => x.AreaM2 <= maxAreaM2);
        }

        if (filters.Bedrooms is { } bedrooms)
        {
            listings = listings.Where(x => x.Bedrooms == bedrooms);
        }

        if (filters.RoomType is { } roomType)
        {
            listings = listings.Where(x => x.RoomType == roomType);
        }

        return listings;
    }
}
