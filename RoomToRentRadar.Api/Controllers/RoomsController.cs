using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RoomsController : ControllerBase
{
    private readonly ISearchRoomsUseCase _searchRoomsUseCase;

    public RoomsController(ISearchRoomsUseCase searchRoomsUseCase)
    {
        ArgumentNullException.ThrowIfNull(searchRoomsUseCase);
        _searchRoomsUseCase = searchRoomsUseCase;
    }

    // [AllowAnonymous] đặt trên action thay vì class: mọi action ghi sau này vẫn mặc định yêu cầu xác thực.
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<RoomCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RoomCardDto>>> SearchRooms(
        [FromQuery] RoomSearchRequest request,
        CancellationToken ct)
    {
        var clampedPageSize = Math.Clamp(request.PageSize, 1, RoomSearchRequest.MaxPageSize);
        var safePage = Math.Max(request.PageNumber, 1);
        var safeRequest = request with { PageNumber = safePage, PageSize = clampedPageSize };

        // Model binding turns `?sort=` into null; restore "present but empty" so the use case can
        // reject it (ruling 21) instead of silently applying the default sort. The HttpContext is
        // absent when the action is unit-tested directly; HTTP behaviour is covered by the
        // integration tests.
        var httpRequest = ControllerContext?.HttpContext?.Request;
        if (safeRequest.Sort is null
            && httpRequest is not null
            && httpRequest.Query.ContainsKey("sort"))
        {
            safeRequest = safeRequest with { Sort = string.Empty };
        }

        var result = await _searchRoomsUseCase.ExecuteAsync(safeRequest, ct);
        return Ok(result);
    }
}
