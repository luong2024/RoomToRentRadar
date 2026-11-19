using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomToRentRadar.Api.Features.Travel;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/listings/{listingId:guid}/travel")]
[EnableRateLimiting(TravelRateLimitPolicy.Name)]
public sealed class ListingTravelController(TravelEstimatesService travelEstimatesService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ListingTravelEstimatesDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<ListingTravelEstimatesDto>> GetEstimates(
        Guid listingId,
        [FromQuery] TravelQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!TravelCampusResolver.TryResolve(
                parameters.CampusId,
                out var campus,
                out var problem))
        {
            return ToProblem(problem!);
        }

        var result = await travelEstimatesService.GetEstimatesAsync(
            listingId,
            campus.Id,
            campus.Lat,
            campus.Lng,
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Problem!);
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
