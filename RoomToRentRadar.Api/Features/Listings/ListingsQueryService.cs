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
/// memory and the cover photos of the selected page come from a second query.
/// </summary>
public sealed class ListingsQueryService(AppDbContext dbContext)
{
    public async Task<ListingSearchResultDto> GetPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var activeFilters = BuildActiveFilters(query);

        return query.Campus is { } campus
            ? await GetRadiusPageAsync(query, campus, cursor, activeFilters, cancellationToken)
            : await GetSqlPageAsync(query, cursor, activeFilters, cancellationToken);
    }

    public async Task<ListingDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == id && x.Status == ListingStatus.Published)
            .Select(x => new ListingDetailDto(
                x.Id,
                x.Title,
                x.Description,
                x.Price,
                x.AreaM2,
                x.Bedrooms,
                x.Address,
                x.Lat,
                x.Lng,
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
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ListingSearchResultDto> GetSqlPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        ActiveListingFiltersDto? activeFilters,
        CancellationToken cancellationToken)
    {
        var listings = ApplyFilters(
            dbContext.Listings
                .AsNoTracking()
                .Where(x => x.Status == ListingStatus.Published),
            query);

        // Counted before the cursor so every page of a walk reports the same total.
        var (totalCount, totalCountCapped) = await CountMatchesAsync(listings, cancellationToken);

        var pageQuery = cursor is null ? listings : ListingSortStrategy.ApplyCursor(listings, cursor);
        pageQuery = ListingSortStrategy.ApplyOrdering(pageQuery, query.Sort);

        var items = await pageQuery
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

        return new ListingSearchResultDto(
            items,
            new PaginationInfo(query.Limit, nextCursor, hasMore),
            totalCount,
            totalCountCapped,
            activeFilters);
    }

    private async Task<ListingSearchResultDto> GetRadiusPageAsync(
        ListingQuery query,
        CampusFilter campus,
        ListingCursor? cursor,
        ActiveListingFiltersDto? activeFilters,
        CancellationToken cancellationToken)
    {
        var box = GeoDistance.BoundingBox(campus.Lat, campus.Lng, campus.MaxDistanceKm);

        var listings = ApplyFilters(
            dbContext.Listings
                .AsNoTracking()
                .Where(x => x.Status == ListingStatus.Published
                    && x.Lat >= box.MinLat
                    && x.Lat <= box.MaxLat
                    && x.Lng >= box.MinLng
                    && x.Lng <= box.MaxLng),
            query);

        // Display columns are projected as well so the page can be built without a
        // third query; photos stay out of this query.
        var candidates = await listings
            .Select(x => new CandidateRow(
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

        var matches = new List<ListingSortKey>(candidates.Count);
        var rows = new Dictionary<Guid, CandidateRow>(candidates.Count);
        foreach (var candidate in candidates)
        {
            // The bounding box is a superset of the circle, so the exact distance
            // decides membership. The boundary is inclusive.
            if (GeoDistance.HaversineKm(campus.Lat, campus.Lng, candidate.Lat, candidate.Lng)
                > campus.MaxDistanceKm)
            {
                continue;
            }

            matches.Add(new ListingSortKey(candidate.Id, candidate.CreatedAt, candidate.Price, candidate.AreaM2));
            rows[candidate.Id] = candidate;
        }

        // Counted before the cursor so every page reports the same total.
        var totalCount = Math.Min(matches.Count, ListingSearchResultDto.TotalCountLimit);
        var totalCountCapped = matches.Count > ListingSearchResultDto.TotalCountLimit;

        var remaining = cursor is null
            ? matches
            : matches.Where(key => ListingSortKeyOrdering.IsAfterCursor(key, cursor)).ToList();

        var ordered = ListingSortKeyOrdering.Sort(remaining, query.Sort);

        var pageKeys = ordered.Take(query.Limit + 1).ToList();
        var hasMore = pageKeys.Count > query.Limit;
        if (hasMore)
        {
            pageKeys.RemoveAt(pageKeys.Count - 1);
        }

        var coverPhotos = await LoadCoverPhotosAsync(pageKeys, cancellationToken);

        var items = new List<ListingSummaryDto>(pageKeys.Count);
        foreach (var key in pageKeys)
        {
            var row = rows[key.Id];
            items.Add(new ListingSummaryDto(
                key.Id,
                row.Title,
                key.Price,
                key.AreaM2,
                row.Bedrooms,
                row.Address,
                row.Lat,
                row.Lng,
                key.CreatedAt,
                coverPhotos.GetValueOrDefault(key.Id)));
        }

        string? nextCursor = null;
        if (hasMore)
        {
            nextCursor = ListingSortKeyOrdering.EncodeCursor(query.Sort, pageKeys[^1]);
        }

        return new ListingSearchResultDto(
            items,
            new PaginationInfo(query.Limit, nextCursor, hasMore),
            totalCount,
            totalCountCapped,
            activeFilters);
    }

    private static IQueryable<Listing> ApplyFilters(IQueryable<Listing> listings, ListingQuery query)
    {
        if (query.MinPrice is { } minPrice)
        {
            listings = listings.Where(x => x.Price >= minPrice);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            listings = listings.Where(x => x.Price <= maxPrice);
        }

        if (query.MinAreaM2 is { } minAreaM2)
        {
            listings = listings.Where(x => x.AreaM2 >= minAreaM2);
        }

        if (query.MaxAreaM2 is { } maxAreaM2)
        {
            listings = listings.Where(x => x.AreaM2 <= maxAreaM2);
        }

        if (query.Bedrooms is { } bedrooms)
        {
            listings = listings.Where(x => x.Bedrooms == bedrooms);
        }

        if (query.RoomType is { } roomType)
        {
            listings = listings.Where(x => x.RoomType == roomType);
        }

        return listings;
    }

    /// <summary>
    /// Counts the matches, capped at <see cref="ListingSearchResultDto.TotalCountLimit"/>
    /// so a huge result set never scans more than the limit plus one row. The provider
    /// fallback keeps the endpoint working if LIMIT cannot be applied to an aggregate.
    /// </summary>
    private static async Task<(int TotalCount, bool TotalCountCapped)> CountMatchesAsync(
        IQueryable<Listing> listings,
        CancellationToken cancellationToken)
    {
        const int limit = ListingSearchResultDto.TotalCountLimit;

        int counted;
        try
        {
            counted = await listings.Take(limit + 1).CountAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            counted = await listings.CountAsync(cancellationToken);
        }

        return counted > limit ? (limit, true) : (counted, false);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadCoverPhotosAsync(
        IReadOnlyList<ListingSortKey> pageKeys,
        CancellationToken cancellationToken)
    {
        if (pageKeys.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var listingIds = pageKeys.Select(key => key.Id).ToList();

        var photos = await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(photo => listingIds.Contains(photo.ListingId))
            .OrderByDescending(photo => photo.IsCover)
            .ThenBy(photo => photo.SortOrder)
            .ThenBy(photo => photo.Id)
            .Select(photo => new { photo.ListingId, Url = photo.ThumbUrl ?? photo.Url })
            .ToListAsync(cancellationToken);

        var coverPhotos = new Dictionary<Guid, string>(photos.Count);
        foreach (var photo in photos)
        {
            // Photos are ordered, so the first row per listing is its cover.
            coverPhotos.TryAdd(photo.ListingId, photo.Url);
        }

        return coverPhotos;
    }

    private static ActiveListingFiltersDto? BuildActiveFilters(ListingQuery query)
    {
        if (query.MinPrice is null
            && query.MaxPrice is null
            && query.MinAreaM2 is null
            && query.MaxAreaM2 is null
            && query.Bedrooms is null
            && query.RoomType is null
            && query.Campus is null)
        {
            return null;
        }

        return new ActiveListingFiltersDto(
            query.MinPrice,
            query.MaxPrice,
            query.MinAreaM2,
            query.MaxAreaM2,
            query.Bedrooms,
            query.RoomType,
            query.Campus?.Lat,
            query.Campus?.Lng,
            query.Campus?.MaxDistanceKm);
    }

    private sealed record CandidateRow(
        Guid Id,
        string Title,
        long Price,
        double AreaM2,
        int Bedrooms,
        string Address,
        double Lat,
        double Lng,
        DateTime CreatedAt);
}
