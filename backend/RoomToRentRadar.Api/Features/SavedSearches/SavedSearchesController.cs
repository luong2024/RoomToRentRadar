using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Ports.Models.SavedSearches;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.UseCases.Listings;

namespace RoomToRentRadar.Api.Features.SavedSearches;

[ApiController]
[Authorize]
[Route("api/v1/saved-searches")]
public sealed class SavedSearchesController(ISavedSearchesUseCase savedSearchesUseCase) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<SavedSearchDto>> CreateSavedSearch(
        [FromBody] CreateSavedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ToProblem(SavedSearchProblemFactory.InvalidNameProblem("name is required."));
        }

        if (name.Length > SavedSearchLimits.MaximumNameLength)
        {
            return ToProblem(SavedSearchProblemFactory.InvalidNameProblem(
                $"name must contain at most {SavedSearchLimits.MaximumNameLength} characters."));
        }

        if (!Enum.IsDefined(request.AlertState))
        {
            return ToProblem(SavedSearchProblemFactory.InvalidAlertStateProblem(
                "alert_state must be off, active or paused."));
        }

        if (!GeoSearch.TryCreateSearchArea(request.Area, out _, out var areaError))
        {
            return ToProblem(AreaSearchProblemFactory.InvalidArea(areaError!));
        }

        var savedSearch = await savedSearchesUseCase.CreateAsync(
            CurrentUserId,
            name,
            request.Area!,
            request.AlertState,
            cancellationToken);
        return Created($"/api/v1/saved-searches/{savedSearch.Id}", savedSearch);
    }

    [HttpPut("{id:guid}/alert")]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SavedSearchDto>> SetSavedSearchAlert(
        Guid id,
        [FromBody] SetSavedSearchAlertRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.AlertState))
        {
            return ToProblem(SavedSearchProblemFactory.InvalidAlertStateProblem(
                "alert_state must be off, active or paused."));
        }

        var savedSearch = await savedSearchesUseCase.SetAlertStateAsync(
            CurrentUserId,
            id,
            request.AlertState,
            cancellationToken);
        return savedSearch is null
            ? ToProblem(SavedSearchProblemFactory.NotFoundProblem())
            : Ok(savedSearch);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteSavedSearch(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await savedSearchesUseCase.DeleteAsync(CurrentUserId, id, cancellationToken);
        return deleted ? NoContent() : ToProblem(SavedSearchProblemFactory.NotFoundProblem());
    }

    private Guid CurrentUserId
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
