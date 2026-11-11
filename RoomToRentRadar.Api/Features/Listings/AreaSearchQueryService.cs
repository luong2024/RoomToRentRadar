using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Area search: one bounding-box candidate query, exact polygon classification in
/// memory, materialized ordering/cursor paging and one cover-photo query for the
/// selected page. At most two SELECT statements are issued.
/// </summary>
public sealed class AreaSearchQueryService(AppDbContext dbContext)
{
    public async Task<AreaSearchResultDto> SearchAsync(
        ListingQuery query,
        SearchArea area,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var candidates = await SelectCandidates(query, area).ToListAsync(cancellationToken);

        var matches = new List<ListingSortKey>(candidates.Count);
        var rows = new Dictionary<Guid, CandidateRow>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!area.Intersects(candidate.Lng, candidate.Lat))
            {
                continue;
            }

            matches.Add(new ListingSortKey(candidate.Id, candidate.CreatedAt, candidate.Price, candidate.AreaM2));
            rows[candidate.Id] = candidate;
        }

        // Counted before the cursor so every page reports the same total.
        var totalCount = Math.Min(matches.Count, AreaSearchResultDto.TotalCountLimit);
        var totalCountCapped = matches.Count > AreaSearchResultDto.TotalCountLimit;

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

        return new AreaSearchResultDto(
            items,
            new PaginationInfo(query.Limit, nextCursor, hasMore),
            totalCount,
            totalCountCapped);
    }

    private IQueryable<CandidateRow> SelectCandidates(ListingQuery query, SearchArea area)
    {
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published
                && x.Lat >= area.MinLat
                && x.Lat <= area.MaxLat
                && x.Lng >= area.MinLng
                && x.Lng <= area.MaxLng);

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

        // Display columns are projected as well so the page can be built without a
        // third query; photos stay out of this query.
        return listings.Select(x => new CandidateRow(
            x.Id,
            x.Title,
            x.Price,
            x.AreaM2,
            x.Bedrooms,
            x.Address,
            x.Lat,
            x.Lng,
            x.CreatedAt));
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
