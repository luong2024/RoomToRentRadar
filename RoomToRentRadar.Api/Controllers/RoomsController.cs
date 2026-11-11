using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RoomsController : ControllerBase
{
    private readonly IGetRoomDetailUseCase _getRoomDetailUseCase;

    public RoomsController(IGetRoomDetailUseCase getRoomDetailUseCase)
    {
        _getRoomDetailUseCase = getRoomDetailUseCase ?? throw new ArgumentNullException(nameof(getRoomDetailUseCase));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDetailDto>> GetRoomById(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Room not found",
                Detail = "Room ID cannot be empty.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var result = await _getRoomDetailUseCase.ExecuteAsync(id, ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Room not found",
                Detail = $"Room listing with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }
}
