using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class ListingsQueryService(AppDbContext dbContext)
{
    public async Task<PagedResult<ListingSummaryDto>> GetPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published);

        if (query.MinPrice is { } minPrice)
        {
            listings = listings.Where(x => x.Price >= minPrice);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            listings = listings.Where(x => x.Price <= maxPrice);
        }

        if (query.Bedrooms is { } bedrooms)
        {
            listings = listings.Where(x => x.Bedrooms == bedrooms);
        }

        if (cursor is not null)
        {
            listings = ListingSortStrategy.ApplyCursor(listings, cursor);
        }

        listings = ListingSortStrategy.ApplyOrdering(listings, query.Sort);

        var items = await listings
            .Take(query.Limit + 1)
            .Select(x => new ListingSummaryDto(
                x.Id,
                x.Title,
                x.Price,
                x.AreaM2,
                x.Bedrooms,
                x.Address,
                x.Lat,
                x.Lng,
                x.CreatedAt,
                x.Photos
                    .OrderByDescending(photo => photo.IsCover)
                    .ThenBy(photo => photo.SortOrder)
                    .ThenBy(photo => photo.Id)
                    .Select(photo => photo.ThumbUrl ?? photo.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > query.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore)
        {
            var last = items[^1];
            nextCursor = ListingCursorCodec.Encode(
                query.Sort,
                last.Id,
                last.CreatedAt,
                last.Price,
                last.AreaM2);
        }

        return new PagedResult<ListingSummaryDto>(
            items,
            new PaginationInfo(query.Limit, nextCursor, hasMore));
    }
}
