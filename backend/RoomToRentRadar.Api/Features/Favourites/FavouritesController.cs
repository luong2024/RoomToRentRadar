using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Api.Features.Favourites;

[ApiController]
[Authorize]
[Route("api/v1/favourites")]
public sealed class FavouritesController(IFavouritesUseCase favouritesUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FavouriteRoomDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<FavouriteRoomDto>>> GetFavourites(
        CancellationToken cancellationToken)
        => Ok(await favouritesUseCase.GetAsync(CurrentUserId, cancellationToken));

    [HttpPut("{listingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> AddFavourite(Guid listingId, CancellationToken cancellationToken)
    {
        var listingExists = await favouritesUseCase.AddAsync(CurrentUserId, listingId, cancellationToken);
        return listingExists ? NoContent() : ToProblem(ListingProblemFactory.NotFound());
    }

    [HttpDelete("{listingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> RemoveFavourite(Guid listingId, CancellationToken cancellationToken)
    {
        await favouritesUseCase.RemoveAsync(CurrentUserId, listingId, cancellationToken);
        return NoContent();
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
