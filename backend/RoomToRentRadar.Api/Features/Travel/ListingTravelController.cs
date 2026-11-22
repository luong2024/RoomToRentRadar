using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Features.Travel;

[ApiController]
[Route("api/v1/listings/{listingId:guid}/travel")]
[EnableRateLimiting(TravelRateLimitPolicy.Name)]
public sealed class ListingTravelController(ITravelUseCase travelUseCase) : ControllerBase
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

        var result = await travelUseCase.GetEstimatesAsync(
            listingId,
            campus.Id,
            campus.Lat,
            campus.Lng,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error!.Code == ListingErrorCodes.NotFound
            ? ToProblem(ListingProblemFactory.NotFound())
            : Problem(statusCode: StatusCodes.Status500InternalServerError);
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
