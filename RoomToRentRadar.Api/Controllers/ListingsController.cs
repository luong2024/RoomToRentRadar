using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Services;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ListingsController(IListingService listingService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách phòng trọ có phân trang + sắp xếp (phục vụ infinite scroll trên app MAUI).
    /// </summary>
    /// <remarks>
    /// GET /api/listings?pageNumber=1&amp;pageSize=10&amp;sortBy=price&amp;isAscending=true
    /// </remarks>
    /// <response code="200">Trả về một trang dữ liệu kèm metadata phân trang.</response>
    /// <response code="400">Tham số phân trang không hợp lệ.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ListingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ListingDto>>> GetListings(
        [FromQuery] ListingQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        // [ApiController] tự trả 400 nếu vi phạm [Range] trên DTO.
        var result = await listingService.GetListingsAsync(parameters, cancellationToken);
        return Ok(result);
    }
}
