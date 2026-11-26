using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RoomsController : ControllerBase
{
    private readonly ISearchRoomsUseCase? _searchRoomsUseCase;
    private readonly IGetRoomDetailUseCase? _getRoomDetailUseCase;
    private readonly IGetSimilarRoomsUseCase? _getSimilarRoomsUseCase;
    private readonly IGetRoomTravelTimesUseCase? _getRoomTravelTimesUseCase;

    [ActivatorUtilitiesConstructor]
    public RoomsController(
        ISearchRoomsUseCase searchRoomsUseCase,
        IGetRoomDetailUseCase getRoomDetailUseCase,
        IGetSimilarRoomsUseCase getSimilarRoomsUseCase,
        IGetRoomTravelTimesUseCase getRoomTravelTimesUseCase)
    {
        ArgumentNullException.ThrowIfNull(searchRoomsUseCase);
        ArgumentNullException.ThrowIfNull(getRoomDetailUseCase);
        ArgumentNullException.ThrowIfNull(getSimilarRoomsUseCase);
        ArgumentNullException.ThrowIfNull(getRoomTravelTimesUseCase);
        _searchRoomsUseCase = searchRoomsUseCase;
        _getRoomDetailUseCase = getRoomDetailUseCase;
        _getSimilarRoomsUseCase = getSimilarRoomsUseCase;
        _getRoomTravelTimesUseCase = getRoomTravelTimesUseCase;
    }

    /// <summary>
    /// Test helper constructor for SearchRooms test suite (T01.1/T04.1).
    /// </summary>
    public RoomsController(ISearchRoomsUseCase searchRoomsUseCase)
    {
        ArgumentNullException.ThrowIfNull(searchRoomsUseCase);
        _searchRoomsUseCase = searchRoomsUseCase;
        _getRoomDetailUseCase = null;
        _getSimilarRoomsUseCase = null;
        _getRoomTravelTimesUseCase = null;
    }

    /// <summary>
    /// Test helper constructor for RoomDetail test suite.
    /// </summary>
    public RoomsController(
        IGetRoomDetailUseCase getRoomDetailUseCase,
        IGetSimilarRoomsUseCase? getSimilarRoomsUseCase = null)
    {
        ArgumentNullException.ThrowIfNull(getRoomDetailUseCase);
        _searchRoomsUseCase = null;
        _getRoomDetailUseCase = getRoomDetailUseCase;
        _getSimilarRoomsUseCase = getSimilarRoomsUseCase;
        _getRoomTravelTimesUseCase = null;
    }

    /// <summary>
    /// Test helper constructor for TravelTimes test suite (T09.1).
    /// </summary>
    public RoomsController(IGetRoomTravelTimesUseCase getRoomTravelTimesUseCase)
    {
        ArgumentNullException.ThrowIfNull(getRoomTravelTimesUseCase);
        _searchRoomsUseCase = null;
        _getRoomDetailUseCase = null;
        _getSimilarRoomsUseCase = null;
        _getRoomTravelTimesUseCase = getRoomTravelTimesUseCase;
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
        if (_searchRoomsUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

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

    /// <summary>
    /// getRoomDetail: Returns full room details per docs/api/openapi.yaml, US02, and BR-AUTH-07.
    /// Supports ETag caching and If-None-Match per ADR-004 and ADR-012.
    /// </summary>
    [HttpGet("{roomId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomById(
        string roomId,
        CancellationToken ct = default,
        [FromQuery] string? campusId = null)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return CreateInvalidRoomIdResult();
        }

        if (_getRoomDetailUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var result = await _getRoomDetailUseCase.ExecuteAsync(roomId, campusId, ct);
        if (result == null)
        {
            return CreateRoomNotFoundResult(roomId);
        }

        // Cache ETag adheres strictly to ADR-012 §6 and RFC 9110 §8.8.3:
        // 1. Weak validator prefix W/"<hash>" computed from caller-specific serialized response body.
        // 2. Cache-Control: private, no-cache to prevent intermediate proxy poisoning.
        // 3. Vary: Authorization because landlord phone visibility differs by auth state (BR-AUTH-07).
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(result);
        var hash = Convert.ToHexString(SHA256.HashData(jsonBytes)).ToLowerInvariant();
        var etag = $"W/\"{hash}\"";

        Response?.Headers?.TryAdd("Cache-Control", "private, no-cache");
        Response?.Headers?.TryAdd("Vary", "Authorization");
        Response?.Headers?.TryAdd("ETag", etag);

        if (Request?.Headers != null && Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatchHeader))
        {
            if (MatchesIfNoneMatch(ifNoneMatchHeader.ToString(), hash))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }

        return Ok(result);
    }

    private static bool MatchesIfNoneMatch(string? ifNoneMatchHeader, string currentHash)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatchHeader))
        {
            return false;
        }

        var trimmed = ifNoneMatchHeader.Trim();
        if (trimmed == "*")
        {
            return true;
        }

        var tags = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var tag in tags)
        {
            var unquoted = tag.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
                ? tag[2..].Trim('\"')
                : tag.Trim('\"');

            if (string.Equals(unquoted, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return CreateInvalidRoomIdResult();
        }

        if (_getSimilarRoomsUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var similarRooms = await _getSimilarRoomsUseCase.ExecuteAsync(roomId, campusId, ct);
        if (similarRooms == null)
        {
            return CreateRoomNotFoundResult(roomId);
        }

        return Ok(similarRooms);
    }

    private BadRequestObjectResult CreateInvalidRoomIdResult()
    {
        var invalidProblem = new ProblemDetails
        {
            Title = "Invalid room request",
            Detail = "Room ID cannot be empty or whitespace.",
            Status = StatusCodes.Status400BadRequest
        };
        invalidProblem.Extensions["errorCode"] = ProblemCodes.ValidationFailed;
        return BadRequest(invalidProblem);
    }

    private NotFoundObjectResult CreateRoomNotFoundResult(string roomId)
    {
        var notFoundProblem = new ProblemDetails
        {
            Title = "Room not found",
            Detail = $"Room listing with ID '{roomId}' was not found.",
            Status = StatusCodes.Status404NotFound
        };
        notFoundProblem.Extensions["errorCode"] = ProblemCodes.RoomNotFound;
        return NotFound(notFoundProblem);
    }

    /// <summary>
    /// getRoomTravelTimes: Returns estimated travel times or straight-line fallback (US09, UC-12, BR-TRV-01..04).
    /// </summary>
    [HttpGet("{roomId}/travel-times")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TravelTimesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TravelTimesDto>> GetRoomTravelTimes(
        string roomId,
        [FromQuery] string? campusId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            var invalidProblem = new ProblemDetails
            {
                Title = "Invalid room request",
                Detail = "Room ID cannot be empty or whitespace.",
                Status = StatusCodes.Status400BadRequest
            };
            invalidProblem.Extensions["errorCode"] = ProblemCodes.ValidationFailed;
            return BadRequest(invalidProblem);
        }

        if (_getRoomTravelTimesUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var result = await _getRoomTravelTimesUseCase.ExecuteAsync(roomId, campusId, ct);
        return Ok(result);
    }
}
