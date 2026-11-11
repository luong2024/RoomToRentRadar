using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/listings")]
public sealed class ListingsController(ListingsQueryService listingsQueryService) : ControllerBase
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

        ListingCursor? cursor = null;
        if (query.Cursor is not null)
        {
            if (!ListingCursorCodec.TryDecode(query.Cursor, out cursor))
            {
                return ToProblem(ListingProblemFactory.InvalidQuery(
                    ListingProblemFactory.InvalidCursor,
                    "cursor is not a valid listings cursor."));
            }

            if (cursor.Sort != query.Sort)
            {
                return ToProblem(ListingProblemFactory.InvalidQuery(
                    ListingProblemFactory.CursorSortMismatch,
                    "cursor does not belong to the requested sort."));
            }
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

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
