using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using AreaSearchOriginInput = RoomToRentRadar.Ports.Models.Listings.AreaSearchOrigin;

namespace RoomToRentRadar.UseCases.Listings;

/// <summary>
/// Area search: validates the drawn ring and the distance origin, one bounding-box
/// candidate query, exact polygon classification in memory, then
/// <see cref="ListingCandidatePageBuilder"/> for ordering, cursor paging and the
/// cover photos of the selected page. At most two SELECT statements.
/// </summary>
public sealed class AreaSearchService(
    IListingsQueryRepository repository,
    ListingCandidatePageBuilder candidatePageBuilder) : IAreaSearchUseCase
{
    public async Task<AreaSearchResult> SearchAsync(
        AreaSearchQuery search,
        CancellationToken cancellationToken)
    {
        if (!GeoSearch.TryCreateSearchArea(search.Area, out var area, out var areaError))
        {
            return AreaSearchResult.Failure(areaError!.Code, areaError.Detail, areaError.Field);
        }

        if (!TryResolveOrigin(search.Origin, area, out var originLat, out var originLng, out var originDetail))
        {
            return AreaSearchResult.Failure(
                AreaSearchErrorCodes.InvalidOrigin, originDetail, "origin");
        }

        // The resolved polygon and origin are part of the paging context: a cursor
        // issued for another drawn area must not be reused for this one.
        var filterFingerprint = ListingFilterFingerprint.ComputeForArea(
            search.Query.Filters, area, originLat, originLng);

        if (!TryResolveCursor(search.Query, filterFingerprint, out var cursor, out var cursorFailure))
        {
            return cursorFailure!;
        }

        var candidates = await repository.GetAreaCandidatesAsync(
            search.Query.Filters,
            new GeoBoundingBox(area.MinLat, area.MaxLat, area.MinLng, area.MaxLng),
            cancellationToken);

        var matches = candidates
            .Where(candidate => area.Intersects(candidate.Lng, candidate.Lat))
            .Select(candidate => candidate with
            {
                DistanceKm = GeoDistance.HaversineKm(originLat, originLng, candidate.Lat, candidate.Lng)
            })
            .ToList();

        var page = await candidatePageBuilder.BuildAsync(
            matches,
            search.Query,
            cursor,
            filterFingerprint,
            cancellationToken);

        return AreaSearchResult.Success(new SearchResultDto<ListingSummaryDto>(
            page.Items,
            page.Pagination,
            page.TotalCount,
            page.TotalCountCapped));
    }

    private static bool TryResolveOrigin(
        AreaSearchOriginInput? origin,
        SearchArea area,
        out double lat,
        out double lng,
        out string detail)
    {
        detail = string.Empty;

        if (origin is null)
        {
            lat = area.CentroidLat;
            lng = area.CentroidLng;
            return true;
        }

        if (origin.Lat is not { } providedLat || origin.Lng is not { } providedLng)
        {
            lat = 0;
            lng = 0;
            detail = "origin must provide both lat and lng.";
            return false;
        }

        if (!double.IsFinite(providedLat) || providedLat is < -90d or > 90d
            || !double.IsFinite(providedLng) || providedLng is < -180d or > 180d)
        {
            lat = 0;
            lng = 0;
            detail = "origin.lat must be within [-90, 90] and origin.lng within [-180, 180].";
            return false;
        }

        lat = providedLat;
        lng = providedLng;
        return true;
    }

    private static bool TryResolveCursor(
        ListingQuery query,
        string filterFingerprint,
        out ListingCursor? cursor,
        out AreaSearchResult? failure)
    {
        cursor = null;
        failure = null;

        if (query.Cursor is null)
        {
            return true;
        }

        if (!ListingCursorCodec.TryDecode(query.Cursor, out var decoded))
        {
            failure = AreaSearchResult.Failure(
                AreaSearchErrorCodes.InvalidCursor,
                "cursor is not a valid listings cursor.");
            return false;
        }

        if (decoded.Sort != query.Sort)
        {
            failure = AreaSearchResult.Failure(
                AreaSearchErrorCodes.CursorSortMismatch,
                "cursor does not belong to the requested sort.");
            return false;
        }

        if (!string.Equals(decoded.FilterFingerprint, filterFingerprint, StringComparison.Ordinal))
        {
            failure = AreaSearchResult.Failure(
                AreaSearchErrorCodes.CursorFilterMismatch,
                "cursor does not belong to the requested filters.");
            return false;
        }

        cursor = decoded;
        return true;
    }
}
