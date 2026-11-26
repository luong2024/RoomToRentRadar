using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.SavedSearches;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Controllers;

/// <summary>
/// US07 / T07.1 — saved search &amp; alert rules. User xác định qua <see cref="ControllerIdentityHelper"/> (JWT hoặc header <c>X-User-Id</c>).
/// </summary>
[ApiController]
[Route("api/v1/saved-searches")]
[Produces("application/json")]
public sealed class SavedSearchesController(ISavedSearchService savedSearchService) : ControllerBase
{
    /// <summary>GET /api/v1/saved-searches — danh sách tìm kiếm đã lưu của user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavedSearchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SavedSearchDto>>> GetMine(CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        return Ok(await savedSearchService.GetMineAsync(userId, cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedSearchDto>> GetById(string id, CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        var result = await savedSearchService.GetAsync(userId, id, cancellationToken);
        return result is null ? NotFoundProblem(id) : Ok(result);
    }

    /// <summary>POST /api/v1/saved-searches — lưu bộ lọc (+ polygon tùy chọn) và bật/tắt alert.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SavedSearchDto>> Create([FromBody] SaveSearchRequestDto request, CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        var created = await savedSearchService.CreateAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedSearchDto>> Update(string id, [FromBody] SaveSearchRequestDto request, CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        var updated = await savedSearchService.UpdateAsync(userId, id, request, cancellationToken);
        return updated is null ? NotFoundProblem(id) : Ok(updated);
    }

    /// <summary>PATCH /api/v1/saved-searches/{id}/alerts — bật/tắt thông báo.</summary>
    [HttpPatch("{id}/alerts")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedSearchDto>> SetAlerts(
        string id,
        [FromBody] ToggleSavedSearchAlertsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        var updated = await savedSearchService.SetAlertsAsync(userId, id, request.AlertsEnabled, cancellationToken);
        return updated is null ? NotFoundProblem(id) : Ok(updated);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        return await savedSearchService.DeleteAsync(userId, id, cancellationToken) ? NoContent() : NotFoundProblem(id);
    }

    /// <summary>GET /api/v1/saved-searches/{id}/matches — chạy lại tìm kiếm đã lưu (pin preview cards).</summary>
    [HttpGet("{id}/matches")]
    [ProducesResponseType(typeof(SavedSearchMatchesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedSearchMatchesDto>> GetMatches(string id, CancellationToken cancellationToken)
    {
        if (!ControllerIdentityHelper.TryResolveUserId(this, out var userId, out var authError)) return authError!;
        var result = await savedSearchService.GetMatchesAsync(userId, id, cancellationToken);
        return result is null ? NotFoundProblem(id) : Ok(result);
    }

    private ObjectResult NotFoundProblem(string id) => Problem(
        title: "Không tìm thấy tìm kiếm đã lưu",
        detail: $"Không tìm thấy tìm kiếm đã lưu '{id}'.",
        statusCode: StatusCodes.Status404NotFound);
}
