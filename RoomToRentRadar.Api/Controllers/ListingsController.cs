using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/listings")]
public sealed class ListingsController(
    ListingsQueryService listingsQueryService,
    AreaSearchQueryService areaSearchQueryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ListingSummaryDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<PagedResult<ListingSummaryDto>>> GetListings(
        [FromQuery] ListingQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!ListingQueryValidator.TryCreate(parameters, out var query, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        if (!TryResolveCursor(query, out var cursor, out var cursorProblem))
        {
            return ToProblem(cursorProblem!);
        }

        var page = await listingsQueryService.GetPageAsync(query, cursor, cancellationToken);
        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ListingDetailDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListingDetailDto>> GetListing(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await listingsQueryService.GetDetailAsync(id, cancellationToken);

        switch (result.Outcome)
        {
            case ListingDetailOutcome.Found:
                return Ok(result.Detail);
            case ListingDetailOutcome.Unavailable:
                return ToProblem(ListingProblemFactory.Unavailable());
            default:
                return ToProblem(ListingProblemFactory.NotFound());
        }
    }

    [Authorize]
    [HttpGet("{id:guid}/contact")]
    [ProducesResponseType(typeof(ListingContactDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListingContactDto>> GetListingContact(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await listingsQueryService.GetContactAsync(id, cancellationToken);

        switch (result.Outcome)
        {
            case ListingDetailOutcome.Found:
                return Ok(result.Contact);
            case ListingDetailOutcome.Unavailable:
                return ToProblem(ListingProblemFactory.Unavailable());
            default:
                return ToProblem(ListingProblemFactory.NotFound());
        }
    }

    [HttpGet("{id:guid}/similar")]
    [ProducesResponseType(typeof(IReadOnlyList<ListingSummaryDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ListingSummaryDto>>> GetSimilarListings(
        Guid id,
        [FromQuery] int limit = 4,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 10)
        {
            return ToProblem(ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidLimit,
                "limit must be between 1 and 10."));
        }

        var similar = await listingsQueryService.GetSimilarAsync(id, limit, cancellationToken);
        if (similar is null)
        {
            return ToProblem(ListingProblemFactory.NotFound());
        }

        return Ok(similar);
    }

    [HttpPost("search/area")]
    [ProducesResponseType(typeof(AreaSearchResultDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<AreaSearchResultDto>> SearchListingsByArea(
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

        if (!ListingQueryValidator.TryCreate(parameters, out var query, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        if (!TryResolveCursor(query, out var cursor, out var cursorProblem))
        {
            return ToProblem(cursorProblem!);
        }

        if (!GeoSearch.TryCreateSearchArea(request.Area, out var area, out var areaError))
        {
            return ToProblem(AreaSearchProblemFactory.InvalidArea(areaError!));
        }

        var result = await areaSearchQueryService.SearchAsync(query, area, cursor, cancellationToken);
        return Ok(result);
    }

    private static bool TryResolveCursor(
        ListingQuery query,
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
