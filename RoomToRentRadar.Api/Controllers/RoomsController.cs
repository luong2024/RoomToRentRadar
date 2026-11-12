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
    private readonly IGetSimilarRoomsUseCase? _getSimilarRoomsUseCase;

    public RoomsController(
        IGetRoomDetailUseCase getRoomDetailUseCase,
        IGetSimilarRoomsUseCase? getSimilarRoomsUseCase = null)
    {
        _getRoomDetailUseCase = getRoomDetailUseCase ?? throw new ArgumentNullException(nameof(getRoomDetailUseCase));
        _getSimilarRoomsUseCase = getSimilarRoomsUseCase;
    }

    /// <summary>
    /// getRoomDetail: Returns full room details per docs/api/openapi.yaml, US02, and BR-AUTH-07.
    /// Supports ETag caching and If-None-Match per ADR-004 and ADR-012.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomById(
        string id,
        CancellationToken ct = default,
        [FromQuery] string? campusId = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            var invalidProblem = new ProblemDetails
            {
                Title = "Invalid room request",
                Detail = "Room ID cannot be empty or whitespace.",
                Status = StatusCodes.Status400BadRequest
            };
            invalidProblem.Extensions["errorCode"] = "VALIDATION_FAILED";
            return BadRequest(invalidProblem);
        }

        var result = await _getRoomDetailUseCase.ExecuteAsync(id, ct);
        if (result == null)
        {
            var notFoundProblem = new ProblemDetails
            {
                Title = "Room not found",
                Detail = $"Room listing with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound
            };
            notFoundProblem.Extensions["errorCode"] = "ROOM_NOT_FOUND";
            return NotFound(notFoundProblem);
        }

        // ADR-004 & ADR-012: Concurrency & caching ETag
        var etag = $"\"{result.Id}_{result.LastUpdatedAtUtc.Ticks}\"";
        if (Request?.Headers != null && Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatchHeader))
        {
            var clientEtag = ifNoneMatchHeader.ToString().Trim();
            if (string.Equals(clientEtag, etag, StringComparison.Ordinal) ||
                string.Equals(clientEtag, $"\"{result.Id}\"", StringComparison.Ordinal) ||
                clientEtag == "*")
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }

        if (Response?.Headers != null)
        {
            Response.Headers["ETag"] = etag;
        }

        return Ok(result);
    }

    /// <summary>
    /// getSimilarRooms: Returns up to 5 similar available rooms nearby per docs/api/openapi.yaml and BR-LST-10.
    /// </summary>
    [HttpGet("{roomId}/similar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<RoomCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilarRooms(
        string roomId,
        CancellationToken ct = default,
        [FromQuery] string? campusId = null)
    {
        if (_getSimilarRoomsUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }
        if (string.IsNullOrWhiteSpace(roomId))
        {
            var invalidProblem = new ProblemDetails
            {
                Title = "Invalid room request",
                Detail = "Room ID cannot be empty or whitespace.",
                Status = StatusCodes.Status400BadRequest
            };
            invalidProblem.Extensions["errorCode"] = "VALIDATION_FAILED";
            return BadRequest(invalidProblem);
        }

        // Verify that target room exists and is viewable
        var targetRoom = await _getRoomDetailUseCase.ExecuteAsync(roomId, ct);
        if (targetRoom == null)
        {
            var notFoundProblem = new ProblemDetails
            {
                Title = "Room not found",
                Detail = $"Room listing with ID '{roomId}' was not found.",
                Status = StatusCodes.Status404NotFound
            };
            notFoundProblem.Extensions["errorCode"] = "ROOM_NOT_FOUND";
            return NotFound(notFoundProblem);
        }

        var similarRooms = await _getSimilarRoomsUseCase.ExecuteAsync(roomId, campusId, ct);
        return Ok(similarRooms);
    }
}
