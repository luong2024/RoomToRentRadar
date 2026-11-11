using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed class ListingsQueryService(AppDbContext dbContext)
{
    private const double SimilarMinPriceRatio = 0.8;
    private const double SimilarMaxPriceRatio = 1.2;
    private const double SimilarMinAreaRatio = 0.7;
    private const double SimilarMaxAreaRatio = 1.3;

    private static readonly Expression<Func<Listing, ListingSummaryDto>> SummaryProjection =
        x => new ListingSummaryDto(
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
                .FirstOrDefault());

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
            .Select(SummaryProjection)
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

    public async Task<ListingDetailResult> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Status,
                Detail = new ListingDetailDto(
                    x.Id,
                    x.Title,
                    x.Description,
                    x.Price,
                    x.Deposit,
                    x.AreaM2,
                    x.RoomType,
                    x.Bedrooms,
                    x.Address,
                    x.Lat,
                    x.Lng,
                    x.Status,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.Amenities
                        .OrderBy(amenity => amenity.SortOrder)
                        .ThenBy(amenity => amenity.Id)
                        .Select(amenity => amenity.Code)
                        .ToList(),
                    x.Rules
                        .OrderBy(rule => rule.SortOrder)
                        .ThenBy(rule => rule.Id)
                        .Select(rule => rule.Text)
                        .ToList(),
                    x.LandlordName)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return new ListingDetailResult(ListingDetailOutcome.NotFound, null);
        }

        if (listing.Status != ListingStatus.Published)
        {
            return new ListingDetailResult(ListingDetailOutcome.Unavailable, null);
        }

        return new ListingDetailResult(ListingDetailOutcome.Found, listing.Detail);
    }

    public async Task<ListingContactResult> GetContactAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.Status,
                Contact = new ListingContactDto(candidate.LandlordName, candidate.LandlordPhone)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return new ListingContactResult(ListingDetailOutcome.NotFound, null);
        }

        if (listing.Status != ListingStatus.Published)
        {
            return new ListingContactResult(ListingDetailOutcome.Unavailable, null);
        }

        return new ListingContactResult(ListingDetailOutcome.Found, listing.Contact);
    }

    public async Task<IReadOnlyList<ListingSummaryDto>?> GetSimilarAsync(
        Guid id,
        int limit,
        CancellationToken cancellationToken)
    {
        var subject = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == id)
            .Select(listing => new { listing.Bedrooms, listing.Price, listing.AreaM2 })
            .SingleOrDefaultAsync(cancellationToken);

        if (subject is null)
        {
            return null;
        }

        var minPrice = (long)Math.Floor(subject.Price * SimilarMinPriceRatio);
        var maxPrice = (long)Math.Ceiling(subject.Price * SimilarMaxPriceRatio);
        var minArea = subject.AreaM2 * SimilarMinAreaRatio;
        var maxArea = subject.AreaM2 * SimilarMaxAreaRatio;

        return await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Published
                && listing.Id != id
                && listing.Bedrooms == subject.Bedrooms
                && listing.Price >= minPrice
                && listing.Price <= maxPrice
                && listing.AreaM2 >= minArea
                && listing.AreaM2 <= maxArea)
            .OrderBy(listing => Math.Abs(listing.Price - subject.Price))
            .ThenBy(listing => listing.Id)
            .Take(limit)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);
    }
}
