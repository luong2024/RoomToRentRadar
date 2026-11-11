using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
public class RoomsController : ControllerBase
{
    private readonly IGetRoomDetailsUseCase _getRoomDetailsUseCase;

    public RoomsController(IGetRoomDetailsUseCase getRoomDetailsUseCase)
    {
        _getRoomDetailsUseCase = getRoomDetailsUseCase ?? throw new ArgumentNullException(nameof(getRoomDetailsUseCase));
    }

    /// <summary>
    /// Lấy thông tin chi tiết của phòng trọ theo ID.
    /// </summary>
    /// <param name="id">Mã định danh của phòng</param>
    /// <param name="cancellationToken">Token huỷ thao tác</param>
    /// <returns>Dữ liệu chi tiết phòng trọ</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoomDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var roomDetails = await _getRoomDetailsUseCase.ExecuteAsync(id, cancellationToken);
            return Ok(roomDetails);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Phòng không tồn tại",
                Detail = ex.Message
            });
        }
    }
}
