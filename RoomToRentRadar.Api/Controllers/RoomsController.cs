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

    [ActivatorUtilitiesConstructor]
    public RoomsController(
        ISearchRoomsUseCase searchRoomsUseCase,
        IGetRoomDetailUseCase? getRoomDetailUseCase = null,
        IGetSimilarRoomsUseCase? getSimilarRoomsUseCase = null)
    {
        ArgumentNullException.ThrowIfNull(searchRoomsUseCase);
        _searchRoomsUseCase = searchRoomsUseCase;
        _getRoomDetailUseCase = getRoomDetailUseCase;
        _getSimilarRoomsUseCase = getSimilarRoomsUseCase;
    }

    public RoomsController(
        IGetRoomDetailUseCase getRoomDetailUseCase,
        IGetSimilarRoomsUseCase? getSimilarRoomsUseCase = null)
    {
        ArgumentNullException.ThrowIfNull(getRoomDetailUseCase);
        _searchRoomsUseCase = null;
        _getRoomDetailUseCase = getRoomDetailUseCase;
        _getSimilarRoomsUseCase = getSimilarRoomsUseCase;
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

        if (_getRoomDetailUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var result = await _getRoomDetailUseCase.ExecuteAsync(id, ct, campusId);
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

        // ADR-012 §6 & OpenAPI getRoomDetail:
        // Cache ETag is weak (W/"<hash>"), computed from the caller's serialized response body,
        // accompanied by Cache-Control: private, no-cache and Vary: Authorization.
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
    [HttpGet("{id}/similar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<RoomCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilarRooms(
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

        if (_getSimilarRoomsUseCase == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var similarRooms = await _getSimilarRoomsUseCase.ExecuteAsync(id, campusId, ct);
        if (similarRooms == null)
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

        return Ok(similarRooms);
    }
}
