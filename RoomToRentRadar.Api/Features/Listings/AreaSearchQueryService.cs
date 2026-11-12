using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Area search: one bounding-box candidate query, exact polygon classification in
/// memory, then <see cref="ListingCandidatePageBuilder"/> for ordering, cursor paging
/// and the cover photos of the selected page. At most two SELECT statements.
/// </summary>
public sealed class AreaSearchQueryService(
    AppDbContext dbContext,
    ListingCandidatePageBuilder candidatePageBuilder)
{
    public async Task<SearchResultDto<ListingSummaryDto>> SearchAsync(
        ListingQuery query,
        SearchArea area,
        double originLat,
        double originLng,
        ListingCursor? cursor,
        CancellationToken cancellationToken)
    {
        var candidates = await SelectCandidates(query, area).ToListAsync(cancellationToken);

        var matches = candidates
            .Where(candidate => area.Intersects(candidate.Lng, candidate.Lat))
            .Select(candidate => candidate with
            {
                DistanceKm = GeoDistance.HaversineKm(originLat, originLng, candidate.Lat, candidate.Lng)
            })
            .ToList();

        var filterFingerprint = ListingFilterFingerprint.ComputeForArea(
            query.Filters, area, originLat, originLng);

        var page = await candidatePageBuilder.BuildAsync(
            matches, query, cursor, filterFingerprint, cancellationToken);

        return new SearchResultDto<ListingSummaryDto>(
            page.Items,
            page.Pagination,
            page.TotalCount,
            page.TotalCountCapped);
    }

    private IQueryable<ListingCandidate> SelectCandidates(ListingQuery query, SearchArea area)
    {
        // Display columns are projected as well so the page can be built without a
        // third query; photos stay out of this query.
        return dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published
                && x.Lat >= area.MinLat
                && x.Lat <= area.MaxLat
                && x.Lng >= area.MinLng
                && x.Lng <= area.MaxLng)
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
                x.CreatedAt));
    }
}
