using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

/// <summary>
/// Các extension LINQ thuần trên <see cref="IQueryable{T}"/>.
/// Chúng chỉ "xây" expression tree – KHÔNG thực thi – nên provider (Cosmos LINQ)
/// sẽ dịch toàn bộ thành SQL: ORDER BY ... OFFSET ... LIMIT ...
/// </summary>
public static class ListingQueryExtensions
{
    public static IQueryable<Listing> ApplySorting(this IQueryable<Listing> query, string? sortBy, bool isAscending)
    {
        // Whitelist trường sort -> tránh sort theo trường tuỳ ý do client gửi lên.
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "price" => isAscending ? query.OrderBy(l => l.Price) : query.OrderByDescending(l => l.Price),
            "area" => isAscending ? query.OrderBy(l => l.Area) : query.OrderByDescending(l => l.Area),
            _ => isAscending ? query.OrderBy(l => l.CreatedAt) : query.OrderByDescending(l => l.CreatedAt),
        };
    }

    public static IQueryable<Listing> ApplyPaging(this IQueryable<Listing> query, int pageNumber, int pageSize)
    {
        var skip = (pageNumber - 1) * pageSize;
        return query.Skip(skip).Take(pageSize);
    }

    public static IQueryable<Listing> ApplyQuery(this IQueryable<Listing> query, ListingQueryParameters p)
        => query.ApplySorting(p.SortBy, p.IsAscending).ApplyPaging(p.PageNumber, p.PageSize);
}
