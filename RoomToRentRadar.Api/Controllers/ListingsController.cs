using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/listings")]
public sealed class ListingsController(
    ListingsQueryService listingsQueryService,
    AreaSearchQueryService areaSearchQueryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ListingSearchResultDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListingSearchResultDto>> GetListings(
        [FromQuery] ListingQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!ListingQueryValidator.TryCreate(parameters, out var query, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        if (!TryResolveCursor(
                query,
                ListingFilterFingerprint.Compute(query.Filters),
                out var cursor,
                out var cursorProblem))
        {
            return ToProblem(cursorProblem!);
        }

        var page = await listingsQueryService.GetPageAsync(query, cursor, cancellationToken);
        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ListingDetailDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListingDetailDto>> GetListing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var listing = await listingsQueryService.GetDetailAsync(id, cancellationToken);
        if (listing is null)
        {
            return ToProblem(ListingProblemFactory.NotFound());
        }

        return Ok(listing);
    }

    [HttpPost("search/area")]
    [ProducesResponseType(typeof(SearchResultDto<ListingSummaryDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<SearchResultDto<ListingSummaryDto>>> SearchListingsByArea(
        [FromBody] AreaSearchRequest request,
        CancellationToken cancellationToken)
    {
        // The body reuses the query-parameter contract and its validator so limit,
        // sort, price range, bedrooms and cursor semantics stay identical.
        var parameters = new ListingQueryParameters
        {
            Limit = request.Limit,
            Cursor = request.Cursor,
            Sort = request.Sort,
            MinPrice = request.Filters?.MinPrice,
            MaxPrice = request.Filters?.MaxPrice,
            Bedrooms = request.Filters?.Bedrooms
        };

        if (!ListingQueryValidator.TryCreate(
                parameters,
                out var query,
                out var validationProblem,
                allowDistanceSort: true))
        {
            return ToProblem(validationProblem!);
        }

        if (!GeoSearch.TryCreateSearchArea(request.Area, out var area, out var areaError))
        {
            return ToProblem(AreaSearchProblemFactory.InvalidArea(areaError!));
        }

        if (!TryResolveOrigin(request.Origin, area, out var originLat, out var originLng, out var originProblem))
        {
            return ToProblem(originProblem!);
        }

        // The resolved polygon and origin are part of the paging context: a cursor
        // issued for another drawn area must not be reused for this one.
        if (!TryResolveCursor(
                query,
                ListingFilterFingerprint.ComputeForArea(query.Filters, area, originLat, originLng),
                out var cursor,
                out var cursorProblem))
        {
            return ToProblem(cursorProblem!);
        }

        var result = await areaSearchQueryService.SearchAsync(
            query, area, originLat, originLng, cursor, cancellationToken);
        return Ok(result);
    }

    private static bool TryResolveOrigin(
        AreaSearchOrigin? origin,
        SearchArea area,
        out double lat,
        out double lng,
        out ProblemDetails? problem)
    {
        problem = null;

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
            problem = AreaSearchProblemFactory.InvalidOrigin(
                "origin must provide both lat and lng.");
            return false;
        }

        if (!double.IsFinite(providedLat) || providedLat is < -90d or > 90d
            || !double.IsFinite(providedLng) || providedLng is < -180d or > 180d)
        {
            lat = 0;
            lng = 0;
            problem = AreaSearchProblemFactory.InvalidOrigin(
                "origin.lat must be within [-90, 90] and origin.lng within [-180, 180].");
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
        out ProblemDetails? problem)
    {
        cursor = null;
        problem = null;

        if (query.Cursor is null)
        {
            return true;
        }

        if (!ListingCursorCodec.TryDecode(query.Cursor, out var decoded))
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidCursor,
                "cursor is not a valid listings cursor.");
            return false;
        }

        if (decoded.Sort != query.Sort)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.CursorSortMismatch,
                "cursor does not belong to the requested sort.");
            return false;
        }

        if (!string.Equals(
                decoded.FilterFingerprint,
                filterFingerprint,
                StringComparison.Ordinal))
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.CursorFilterMismatch,
                "cursor does not belong to the requested filters.");
            return false;
        }

        cursor = decoded;
        return true;
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
