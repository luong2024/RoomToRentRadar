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

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
