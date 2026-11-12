using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Listing search. Without a campus filter the database applies every filter, counts
/// the matches and returns the page: two SELECT statements. With a campus filter one
/// bounding-box candidate query is issued, the exact haversine distance is applied in
/// memory and <see cref="ListingCandidatePageBuilder"/> builds the page with its
/// cover photos.
/// </summary>
public sealed class ListingsQueryService(
    AppDbContext dbContext,
    ListingCandidatePageBuilder candidatePageBuilder)
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

    public async Task<ListingSearchResultDto> GetPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var activeFilters = BuildActiveFilters(query.Filters);

        if (query.Filters.Campus is not { } campus)
        {
            var page = await GetSqlPageAsync(query, cursor, cancellationToken);
            return new ListingSearchResultDto(
                page.Items,
                page.Pagination,
                page.TotalCount,
                page.TotalCountCapped,
                activeFilters);
        }

        var candidates = await GetRadiusCandidatesAsync(query, campus, cancellationToken);
        var radiusPage = await candidatePageBuilder.BuildAsync(
            candidates,
            query,
            cursor,
            ListingFilterFingerprint.Compute(query.Filters),
            cancellationToken);

        return new ListingSearchResultDto(
            radiusPage.Items,
            radiusPage.Pagination,
            radiusPage.TotalCount,
            radiusPage.TotalCountCapped,
            activeFilters);
    }

    private async Task<ListingCandidatePage> GetSqlPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published)
            .ApplyFilters(query.Filters);

        // Counted before the cursor so every page of a walk reports the same total.
        var counted = await listings
            .Take(SearchLimits.TotalCountLimit + 1)
            .CountAsync(cancellationToken);
        var totalCount = Math.Min(counted, SearchLimits.TotalCountLimit);
        var totalCountCapped = counted > SearchLimits.TotalCountLimit;

        var pageQuery = cursor is null ? listings : ListingSortStrategy.ApplyCursor(listings, cursor);
        pageQuery = ListingSortStrategy.ApplyOrdering(pageQuery, query.Sort);

        var items = await pageQuery
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
                ListingFilterFingerprint.Compute(query.Filters),
                last.CreatedAt,
                last.Price,
                last.AreaM2);
        }

        return new ListingCandidatePage(
            items,
            new PaginationInfo(query.Limit, nextCursor, hasMore),
            totalCount,
            totalCountCapped);
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

    private async Task<IReadOnlyList<ListingCandidate>> GetRadiusCandidatesAsync(
        ListingQuery query,
        CampusFilter campus,
        CancellationToken cancellationToken)
    {
        var box = GeoDistance.BoundingBox(campus.Lat, campus.Lng, campus.MaxDistanceKm);

        var candidates = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published
                && x.Lat >= box.MinLat
                && x.Lat <= box.MaxLat
                && x.Lng >= box.MinLng
                && x.Lng <= box.MaxLng)
            .ApplyFilters(query.Filters)
            .Select(x => new ListingCandidate(
                x.Id,
                x.Title,
                x.Price,
                x.AreaM2,
                x.Bedrooms,
                x.Address,
                x.Lat,
                x.Lng,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        // The bounding box is a superset of the circle, so the exact distance
        // decides membership. The boundary is inclusive.
        return candidates
            .Where(candidate => GeoDistance.HaversineKm(
                campus.Lat, campus.Lng, candidate.Lat, candidate.Lng) <= campus.MaxDistanceKm)
            .ToList();
    }

    private static ActiveListingFiltersDto? BuildActiveFilters(ListingFilters filters)
        => filters.IsEmpty
            ? null
            : new ActiveListingFiltersDto(
                filters.MinPrice,
                filters.MaxPrice,
                filters.MinAreaM2,
                filters.MaxAreaM2,
                filters.Bedrooms,
                filters.RoomType,
                filters.Campus?.Lat,
                filters.Campus?.Lng,
                filters.Campus?.MaxDistanceKm);
}
