using System.Globalization;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.UseCases.Listings;

/// <summary>
/// Listing search. Without a campus filter the repository applies every filter,
/// counts the matches and returns the page: two SELECT statements. With a campus
/// filter one bounding-box candidate query is issued, the exact haversine distance
/// is applied in memory and <see cref="ListingCandidatePageBuilder"/> builds the
/// page with its cover photos.
/// </summary>
public sealed class ListingsQueryService(
    IListingsQueryRepository repository,
    ListingCandidatePageBuilder candidatePageBuilder) : IListingsUseCase
{
    private const double SimilarMinPriceRatio = 0.8;
    private const double SimilarMaxPriceRatio = 1.2;
    private const double SimilarMinAreaRatio = 0.7;
    private const double SimilarMaxAreaRatio = 1.3;

    public async Task<ListingSearchResultDto> GetPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var activeFilters = BuildActiveFilters(query.Filters);

        if (query.Filters.Campus is not { } campus)
        {
            var page = await GetSqlPageAsync(query, cursor, cancellationToken);
            return ToSearchResult(page, activeFilters);
        }

        var candidates = await GetRadiusCandidatesAsync(query, campus, cancellationToken);
        var radiusPage = await candidatePageBuilder.BuildAsync(
            candidates,
            query,
            cursor,
            ListingFilterFingerprint.Compute(query.Filters),
            cancellationToken);

        return ToSearchResult(radiusPage, activeFilters);
    }

    public async Task<ListingDetailResult> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var snapshot = await repository.GetDetailAsync(id, cancellationToken);

        if (snapshot is null)
        {
            return new ListingDetailResult(ListingDetailOutcome.NotFound, null);
        }

        if (snapshot.Status != ListingStatus.Published)
        {
            return new ListingDetailResult(ListingDetailOutcome.Unavailable, null);
        }

        return new ListingDetailResult(ListingDetailOutcome.Found, snapshot.Detail);
    }

    public async Task<ListingContactResult> GetContactAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var snapshot = await repository.GetContactAsync(id, cancellationToken);

        if (snapshot is null)
        {
            return new ListingContactResult(ListingDetailOutcome.NotFound, null);
        }

        if (snapshot.Status != ListingStatus.Published)
        {
            return new ListingContactResult(ListingDetailOutcome.Unavailable, null);
        }

        return new ListingContactResult(ListingDetailOutcome.Found, snapshot.Contact);
    }

    public async Task<IReadOnlyList<ListingSummaryDto>?> GetSimilarAsync(
        Guid id,
        int limit,
        CancellationToken cancellationToken)
    {
        var subject = await repository.GetSimilarSubjectAsync(id, cancellationToken);
        if (subject is null)
        {
            return null;
        }

        var minPrice = (long)Math.Floor(subject.Price * SimilarMinPriceRatio);
        var maxPrice = (long)Math.Ceiling(subject.Price * SimilarMaxPriceRatio);
        var minArea = subject.AreaM2 * SimilarMinAreaRatio;
        var maxArea = subject.AreaM2 * SimilarMaxAreaRatio;

        return await repository.GetSimilarAsync(
            subject,
            minPrice,
            maxPrice,
            minArea,
            maxArea,
            limit,
            cancellationToken);
    }

    private async Task<ListingCandidatePage> GetSqlPageAsync(
        ListingQuery query,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        // Counted before the cursor so every page of a walk reports the same total.
        var counted = await repository.CountPublishedAsync(
            query.Filters,
            SearchLimits.TotalCountLimit,
            cancellationToken);
        var totalCount = Math.Min(counted, SearchLimits.TotalCountLimit);
        var totalCountCapped = counted > SearchLimits.TotalCountLimit;

        var items = (await repository.GetPublishedPageAsync(
                query.Filters,
                query.Sort,
                cursor,
                query.Limit + 1,
                cancellationToken))
            .ToList();

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

    private async Task<IReadOnlyList<ListingCandidate>> GetRadiusCandidatesAsync(
        ListingQuery query,
        CampusFilter campus,
        CancellationToken cancellationToken)
    {
        var box = GeoDistance.BoundingBox(campus.Lat, campus.Lng, campus.MaxDistanceKm);

        var candidates = await repository.GetRadiusCandidatesAsync(
            query.Filters,
            box,
            cancellationToken);

        // The bounding box is a superset of the circle, so the exact distance
        // decides membership. The boundary is inclusive.
        return candidates
            .Where(candidate => GeoDistance.HaversineKm(
                campus.Lat, campus.Lng, candidate.Lat, candidate.Lng) <= campus.MaxDistanceKm)
            .ToList();
    }

    private static ListingSearchResultDto ToSearchResult(
        ListingCandidatePage page,
        ActiveListingFiltersDto? activeFilters)
        => new(
            page.Items,
            page.Pagination,
            page.TotalCount,
            page.TotalCountCapped,
            activeFilters);

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
