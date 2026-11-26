using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Search;

/// <summary>
/// Sắp xếp tin đăng theo <see cref="ListingSortKeys"/> — dùng chung cho T04.1 (search) và T05.2 (spatial).
/// Thêm kiểu sắp xếp mới = thêm một dòng vào bảng <see cref="Sorters"/>, không sửa logic gọi (OCP).
/// </summary>
public static class ListingSorter
{
    private static readonly Func<IEnumerable<ListingDocument>, IOrderedEnumerable<ListingDocument>> ByRentAsc =
        listings => listings.OrderBy(l => l.Rent).ThenBy(l => l.DistanceMeters);

    private static readonly Func<IEnumerable<ListingDocument>, IOrderedEnumerable<ListingDocument>> ByRentDesc =
        listings => listings.OrderByDescending(l => l.Rent).ThenBy(l => l.DistanceMeters);

    private static readonly Func<IEnumerable<ListingDocument>, IOrderedEnumerable<ListingDocument>> ByAreaDesc =
        listings => listings.OrderByDescending(l => l.Area).ThenBy(l => l.DistanceMeters);

    private static readonly Func<IEnumerable<ListingDocument>, IOrderedEnumerable<ListingDocument>> ByDistance =
        listings => listings.OrderBy(l => l.DistanceMeters).ThenBy(l => l.Rent);

    private static readonly Dictionary<string, Func<IEnumerable<ListingDocument>, IOrderedEnumerable<ListingDocument>>> Sorters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ListingSortKeys.Distance] = ByDistance,
            [ListingSortKeys.Rent] = ByRentAsc,
            [ListingSortKeys.RentAsc] = ByRentAsc,
            [ListingSortKeys.Price] = ByRentAsc,
            [ListingSortKeys.PriceAsc] = ByRentAsc,
            [ListingSortKeys.RentDesc] = ByRentDesc,
            [ListingSortKeys.PriceDesc] = ByRentDesc,
            [ListingSortKeys.Area] = ByAreaDesc,
            [ListingSortKeys.AreaDesc] = ByAreaDesc
        };

    /// <summary>sortBy rỗng hoặc không hỗ trợ =&gt; gần campus nhất trước.</summary>
    public static IOrderedEnumerable<ListingDocument> Apply(IEnumerable<ListingDocument> listings, string? sortBy)
    {
        var key = sortBy?.Trim() ?? string.Empty;
        return Sorters.TryGetValue(key, out var sorter) ? sorter(listings) : ByDistance(listings);
    }
}
