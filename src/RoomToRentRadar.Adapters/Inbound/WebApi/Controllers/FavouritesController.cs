using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/v1/favourites")]
[Route("api/favourites")]
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
[Produces("application/json")]
public sealed class FavouritesController : ControllerBase
{
    private readonly IGetMyFavouritesUseCase _getMyFavouritesUseCase;
    private readonly IGetMyFavouriteIdsUseCase _getMyFavouriteIdsUseCase;
    private readonly IAddFavouriteUseCase _addFavouriteUseCase;
    private readonly IRemoveFavouriteUseCase _removeFavouriteUseCase;
    private readonly ISyncFavouritesUseCase _syncFavouritesUseCase;

    public FavouritesController(
        IGetMyFavouritesUseCase getMyFavouritesUseCase,
        IGetMyFavouriteIdsUseCase getMyFavouriteIdsUseCase,
        IAddFavouriteUseCase addFavouriteUseCase,
        IRemoveFavouriteUseCase removeFavouriteUseCase,
        ISyncFavouritesUseCase syncFavouritesUseCase)
    {
        _getMyFavouritesUseCase = getMyFavouritesUseCase ?? throw new ArgumentNullException(nameof(getMyFavouritesUseCase));
        _getMyFavouriteIdsUseCase = getMyFavouriteIdsUseCase ?? throw new ArgumentNullException(nameof(getMyFavouriteIdsUseCase));
        _addFavouriteUseCase = addFavouriteUseCase ?? throw new ArgumentNullException(nameof(addFavouriteUseCase));
        _removeFavouriteUseCase = removeFavouriteUseCase ?? throw new ArgumentNullException(nameof(removeFavouriteUseCase));
        _syncFavouritesUseCase = syncFavouritesUseCase ?? throw new ArgumentNullException(nameof(syncFavouritesUseCase));
    }

    [HttpGet]
    [ProducesResponseType(typeof(FavouriteListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFavourites(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var result = await _getMyFavouritesUseCase.ExecuteAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("ids")]
    [ProducesResponseType(typeof(FavouriteIdsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFavouriteIds(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var result = await _getMyFavouriteIdsUseCase.ExecuteAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FavouriteRoomItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavouriteFromBody(
        [FromBody] AddFavouriteRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.RoomId))
        {
            throw new RequestValidationException("roomId", "RoomId must not be empty or whitespace.");
        }

        var result = await _addFavouriteUseCase.ExecuteAsync(userId, request.RoomId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{*roomId}")]
    [HttpPut("{*roomId}")]
    [ProducesResponseType(typeof(FavouriteRoomItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavouriteByRoute(
        [FromRoute] string? roomId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var decodedRoomId = DecodeRouteRoomId(roomId);
        var result = await _addFavouriteUseCase.ExecuteAsync(userId, decodedRoomId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{*roomId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveFavourite(
        [FromRoute] string? roomId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var decodedRoomId = DecodeRouteRoomId(roomId);
        await _removeFavouriteUseCase.ExecuteAsync(userId, decodedRoomId, cancellationToken);
        return NoContent();
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncFavouritesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SyncFavourites(
        [FromBody] SyncFavouritesRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var result = await _syncFavouritesUseCase.ExecuteAsync(
            userId,
            request?.RoomIds,
            cancellationToken);

        return Ok(result);
    }

    private static string DecodeRouteRoomId(string? roomId)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            return string.Empty;
        }

        try
        {
            return Uri.UnescapeDataString(roomId);
        }
        catch (UriFormatException)
        {
            return roomId;
        }
    }

    private bool TryGetCurrentUserId(out Guid userId, out IActionResult? failureResult)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out userId) || userId == Guid.Empty)
        {
            userId = Guid.Empty;
            failureResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "A valid user identifier (GUID) is missing or malformed in the security token.",
                Instance = HttpContext.Request.Path
            });
            return false;
        }

        failureResult = null;
        return true;
    }
}
