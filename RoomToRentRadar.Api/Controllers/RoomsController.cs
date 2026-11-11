using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class RoomsController : ControllerBase
{
    private readonly ISearchRoomsUseCase _searchRoomsUseCase;

    public RoomsController(ISearchRoomsUseCase searchRoomsUseCase)
    {
        ArgumentNullException.ThrowIfNull(searchRoomsUseCase);
        _searchRoomsUseCase = searchRoomsUseCase;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoomCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RoomCardDto>>> SearchRooms(
        [FromQuery] RoomSearchRequest request,
        CancellationToken ct)
    {
        var clampedPageSize = Math.Clamp(request.PageSize, 1, 50);
        var safePage = Math.Max(request.Page, 1);
        var safeRequest = request with { Page = safePage, PageSize = clampedPageSize };

        var result = await _searchRoomsUseCase.ExecuteAsync(safeRequest, ct);
        return Ok(result);
    }
}
