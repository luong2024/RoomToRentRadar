namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;

[ApiController]
[Route("api/v1/rooms")]
public sealed class RoomListingsController : ControllerBase
{
    private readonly IGetRoomListingsUseCase _getRoomListingsUseCase;

    public RoomListingsController(IGetRoomListingsUseCase getRoomListingsUseCase)
    {
        _getRoomListingsUseCase = getRoomListingsUseCase;
    }

    /// <summary>
    /// Browses verified rental room listings near a student campus (US01 / T01.1).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoomListingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomsAsync(
        [FromQuery] GetRoomListingsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _getRoomListingsUseCase.ExecuteAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            var problemDetails = new ValidationProblemDetails(ex.Errors.ToDictionary(k => k.Key, v => v.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = ex.Message
            };

            return BadRequest(problemDetails);
        }
        catch (CampusNotFoundException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Campus Not Found",
                Detail = ex.Message
            };

            return NotFound(problemDetails);
        }
    }
}
