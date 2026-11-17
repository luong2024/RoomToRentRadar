using Microsoft.AspNetCore.Authorization;
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
    private readonly ISearchRoomsUseCase _searchRoomsUseCase;

    public RoomsController(
        IGetRoomDetailsUseCase getRoomDetailsUseCase,
        ISearchRoomsUseCase searchRoomsUseCase)
    {
        _getRoomDetailsUseCase = getRoomDetailsUseCase ?? throw new ArgumentNullException(nameof(getRoomDetailsUseCase));
        _searchRoomsUseCase = searchRoomsUseCase ?? throw new ArgumentNullException(nameof(searchRoomsUseCase));
    }

    /// <summary>
    /// Tìm kiếm danh sách phòng trọ theo các tiêu chí lọc (giá, diện tích, loại phòng, từ khóa, bán kính/khoảng cách).
    /// </summary>
    /// <param name="query">Các tham số lọc và phân trang</param>
    /// <param name="cancellationToken">Token huỷ thao tác</param>
    /// <returns>Kết quả tìm kiếm bao gồm danh sách phòng, tổng số lượng, số trang và các bộ lọc đang kích hoạt</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SearchRoomsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] SearchRoomsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _searchRoomsUseCase.ExecuteAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Yêu cầu không hợp lệ",
                Detail = ex.Message
            });
        }
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
            var isUserVerified = User.HasClaim(c => c.Type == "IsVerified" && c.Value == "true");

            var roomDetails = await _getRoomDetailsUseCase.ExecuteAsync(id, isUserVerified, cancellationToken);
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
