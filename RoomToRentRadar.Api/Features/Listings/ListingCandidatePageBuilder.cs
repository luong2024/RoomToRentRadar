using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// A published listing that already passed the location membership test (radius or
/// polygon). Members are materialized before paging, so every column needed to
/// build the page travels with them.
/// </summary>
public sealed record ListingCandidate(
    Guid Id,
    string Title,
    long Price,
    double AreaM2,
    int Bedrooms,
    string Address,
    double Lat,
    double Lng,
    DateTime CreatedAt);

public sealed record ListingCandidatePage(
    IReadOnlyList<ListingSummaryDto> Items,
    PaginationInfo Pagination,
    int TotalCount,
    bool TotalCountCapped);

/// <summary>
/// Turns already-filtered location candidates into a cursor page: caps the count,
/// applies the cursor predicate, orders the rows and loads the page's cover photos
/// in one query. Shared by radius and area search so both keep identical paging
/// semantics.
/// </summary>
public sealed class ListingCandidatePageBuilder(AppDbContext dbContext)
{
    public async Task<ListingCandidatePage> BuildAsync(
        IReadOnlyList<ListingCandidate> candidates,
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        // Counted before the cursor so every page of a walk reports the same total.
        var totalCount = Math.Min(candidates.Count, SearchLimits.TotalCountLimit);
        var totalCountCapped = candidates.Count > SearchLimits.TotalCountLimit;

        var rows = new Dictionary<Guid, ListingCandidate>(candidates.Count);
        var keys = new List<ListingSortKey>(candidates.Count);
        foreach (var candidate in candidates)
        {
            rows[candidate.Id] = candidate;
            keys.Add(new ListingSortKey(
                candidate.Id,
                candidate.CreatedAt,
                candidate.Price,
                candidate.AreaM2));
        }

        var remaining = cursor is null
            ? keys
            : keys.Where(key => ListingSortKeyOrdering.IsAfterCursor(key, cursor)).ToList();

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
            nextCursor = ListingSortKeyOrdering.EncodeCursor(
                query.Sort,
                pageKeys[^1],
                ListingFilterFingerprint.Compute(query.Filters));
        }

        return new ListingCandidatePage(
            items,
            new PaginationInfo(query.Limit, nextCursor, hasMore),
            totalCount,
            totalCountCapped);
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
}
