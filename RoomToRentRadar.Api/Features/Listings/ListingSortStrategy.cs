using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingSortStrategy
{
    public static IQueryable<Listing> ApplyOrdering(IQueryable<Listing> query, ListingSort sort) => sort switch
    {
        ListingSort.Newest => query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id),
        ListingSort.PriceAsc => query
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Id),
        ListingSort.PriceDesc => query
            .OrderByDescending(x => x.Price)
            .ThenBy(x => x.Id),
        ListingSort.AreaDesc => query
            .OrderByDescending(x => x.AreaM2)
            .ThenBy(x => x.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
    };

    public static IQueryable<Listing> ApplyCursor(IQueryable<Listing> query, ListingCursor cursor)
    {
        var id = cursor.Id;

        return cursor.Sort switch
        {
            ListingSort.Newest => ApplyNewestCursor(query, cursor, id),
            ListingSort.PriceAsc => ApplyPriceCursor(query, cursor, id, ascending: true),
            ListingSort.PriceDesc => ApplyPriceCursor(query, cursor, id, ascending: false),
            ListingSort.AreaDesc => ApplyAreaCursor(query, cursor, id),
            _ => throw new ArgumentOutOfRangeException(nameof(cursor), cursor.Sort, null)
        };
    }

    private static IQueryable<Listing> ApplyNewestCursor(IQueryable<Listing> query, ListingCursor cursor, Guid id)
    {
        var createdAt = cursor.CreatedAt!.Value;
        return query.Where(x =>
            x.CreatedAt < createdAt
            || (x.CreatedAt == createdAt && x.Id.CompareTo(id) > 0));
    }

    private static IQueryable<Listing> ApplyPriceCursor(
        IQueryable<Listing> query,
        ListingCursor cursor,
        Guid id,
        bool ascending)
    {
        var price = cursor.Price!.Value;
        return ascending
            ? query.Where(x =>
                x.Price > price
                || (x.Price == price && x.Id.CompareTo(id) > 0))
            : query.Where(x =>
                x.Price < price
                || (x.Price == price && x.Id.CompareTo(id) > 0));
    }

    private static IQueryable<Listing> ApplyAreaCursor(IQueryable<Listing> query, ListingCursor cursor, Guid id)
    {
        var areaM2 = cursor.AreaM2!.Value;
        return query.Where(x =>
            x.AreaM2 < areaM2
            || (x.AreaM2 == areaM2 && x.Id.CompareTo(id) > 0));
    }
}
