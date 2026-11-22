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
    private readonly ISearchRoomsInPolygonUseCase? _searchRoomsInPolygonUseCase;

    public RoomListingsController(
        IGetRoomListingsUseCase getRoomListingsUseCase,
        ISearchRoomsInPolygonUseCase? searchRoomsInPolygonUseCase = null)
    {
        _getRoomListingsUseCase = getRoomListingsUseCase ?? throw new ArgumentNullException(nameof(getRoomListingsUseCase));
        _searchRoomsInPolygonUseCase = searchRoomsInPolygonUseCase;
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

    /// <summary>
    /// Searches for verified room listings within a drawn geographic polygon boundary (US05 / T05.2).
    /// </summary>
    /// <param name="request">Polygon boundary coordinates and optional price/room filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of matching room listings with coordinates for map pin display.</returns>
    [HttpPost("spatial-search")]
    [ProducesResponseType(typeof(PagedResult<SpatialRoomSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SpatialSearchAsync(
        [FromBody] SpatialRoomSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        if (_searchRoomsInPolygonUseCase is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Status = StatusCodes.Status501NotImplemented,
                Title = "Service Unavailable",
                Detail = "Spatial search service is not configured."
            });
        }

        try
        {
            var result = await _searchRoomsInPolygonUseCase.ExecuteAsync(request, cancellationToken);
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
    }
}
