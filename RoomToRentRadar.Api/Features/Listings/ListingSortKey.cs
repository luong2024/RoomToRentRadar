using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// The columns needed to order and page materialized area-search candidates. It
/// mirrors the subset of <c>Listing</c> used by <see cref="ListingSortStrategy"/>.
/// </summary>
public readonly record struct ListingSortKey(
    Guid Id,
    DateTime CreatedAt,
    long Price,
    double AreaM2);

/// <summary>
/// In-memory counterpart of <see cref="ListingSortStrategy"/> for sequences that
/// are already materialized (area search). Ordering and the cursor predicate use
/// the same primary key direction and ascending <see cref="Guid.CompareTo(Guid)"/>
/// tie-breaker as the list endpoint, applied in memory instead of SQL.
/// </summary>
public static class ListingSortKeyOrdering
{
    public static IComparer<ListingSortKey> Comparer(ListingSort sort) => sort switch
    {
        ListingSort.Newest => new SortKeyComparer(ListingSort.Newest),
        ListingSort.PriceAsc => new SortKeyComparer(ListingSort.PriceAsc),
        ListingSort.PriceDesc => new SortKeyComparer(ListingSort.PriceDesc),
        ListingSort.AreaDesc => new SortKeyComparer(ListingSort.AreaDesc),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
    };

    public static List<ListingSortKey> Sort(IEnumerable<ListingSortKey> keys, ListingSort sort)
    {
        var ordered = keys.ToList();
        ordered.Sort(Comparer(sort));
        return ordered;
    }

    /// <summary>
    /// True when <paramref name="key"/> sorts after the cursor row for the cursor's
    /// sort. Mirrors the SQL predicate of <see cref="ListingSortStrategy.ApplyCursor"/>.
    /// </summary>
    public static bool IsAfterCursor(ListingSortKey key, ListingCursor cursor)
    {
        var idComparison = key.Id.CompareTo(cursor.Id);

        return cursor.Sort switch
        {
            ListingSort.Newest =>
                key.CreatedAt < cursor.CreatedAt!.Value
                || (key.CreatedAt == cursor.CreatedAt.Value && idComparison > 0),
            ListingSort.PriceAsc =>
                key.Price > cursor.Price!.Value
                || (key.Price == cursor.Price.Value && idComparison > 0),
            ListingSort.PriceDesc =>
                key.Price < cursor.Price!.Value
                || (key.Price == cursor.Price.Value && idComparison > 0),
            ListingSort.AreaDesc =>
                key.AreaM2 < cursor.AreaM2!.Value
                || (key.AreaM2 == cursor.AreaM2.Value && idComparison > 0),
            _ => throw new ArgumentOutOfRangeException(nameof(cursor), cursor.Sort, null)
        };
    }

    public static string EncodeCursor(ListingSort sort, ListingSortKey key)
        => ListingCursorCodec.Encode(sort, key.Id, key.CreatedAt, key.Price, key.AreaM2);

    private sealed class SortKeyComparer(ListingSort sort) : IComparer<ListingSortKey>
    {
        public int Compare(ListingSortKey x, ListingSortKey y)
        {
            var primary = sort switch
            {
                ListingSort.Newest => y.CreatedAt.CompareTo(x.CreatedAt),
                ListingSort.PriceAsc => x.Price.CompareTo(y.Price),
                ListingSort.PriceDesc => y.Price.CompareTo(x.Price),
                ListingSort.AreaDesc => y.AreaM2.CompareTo(x.AreaM2),
                _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
            };

            return primary != 0 ? primary : x.Id.CompareTo(y.Id);
        }
    }
}
