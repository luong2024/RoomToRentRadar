using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/v1/rooms/{roomId}/house-rules")]
[Produces("application/json")]
public sealed class HouseRulesController : ControllerBase
{
    private readonly IGetRoomHouseRulesUseCase _getHouseRulesUseCase;
    private readonly IGetHouseRulesHistoryUseCase _getHouseRulesHistoryUseCase;
    private readonly IUpdateRoomHouseRulesUseCase _updateHouseRulesUseCase;
    private readonly IAssignRoomLandlordUseCase? _assignRoomLandlordUseCase;

    public HouseRulesController(
        IGetRoomHouseRulesUseCase getHouseRulesUseCase,
        IGetHouseRulesHistoryUseCase getHouseRulesHistoryUseCase,
        IUpdateRoomHouseRulesUseCase updateHouseRulesUseCase,
        IAssignRoomLandlordUseCase? assignRoomLandlordUseCase = null)
    {
        _getHouseRulesUseCase = getHouseRulesUseCase ?? throw new ArgumentNullException(nameof(getHouseRulesUseCase));
        _getHouseRulesHistoryUseCase = getHouseRulesHistoryUseCase ?? throw new ArgumentNullException(nameof(getHouseRulesHistoryUseCase));
        _updateHouseRulesUseCase = updateHouseRulesUseCase ?? throw new ArgumentNullException(nameof(updateHouseRulesUseCase));
        _assignRoomLandlordUseCase = assignRoomLandlordUseCase;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomHouseRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentRules(
        [FromRoute] string roomId,
        CancellationToken cancellationToken)
    {
        var result = await _getHouseRulesUseCase.ExecuteAsync(roomId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("versions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<HouseRulesVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersionHistory(
        [FromRoute] string roomId,
        CancellationToken cancellationToken)
    {
        var result = await _getHouseRulesHistoryUseCase.ExecuteAsync(roomId, cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
    [ProducesResponseType(typeof(RoomHouseRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRules(
        [FromRoute] string roomId,
        [FromBody] UpdateHouseRulesRequest request,
        CancellationToken cancellationToken)
    {
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subClaim) || !Guid.TryParse(subClaim, out var actorUserId) || actorUserId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "A valid user identifier is missing from the security token.",
                Instance = HttpContext.Request.Path
            });
        }

        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value
            ?? UserRoleExtensions.LandlordRoleName;

        var command = new UpdateHouseRulesCommand(
            roomId,
            actorUserId,
            roleClaim,
            request.ExpectedVersion,
            request.QuietHours,
            request.GuestPolicy,
            request.CookingRules,
            request.ParkingRules,
            request.AdditionalRules,
            request.ChangeNote);

        var result = await _updateHouseRulesUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("~/api/v1/rooms/{roomId}/landlord")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignLandlord(
        [FromRoute] string roomId,
        [FromBody] AssignRoomLandlordRequest request,
        CancellationToken cancellationToken)
    {
        if (_assignRoomLandlordUseCase is null)
        {
            throw new InvalidOperationException("AssignRoomLandlordUseCase is not configured.");
        }

        await _assignRoomLandlordUseCase.ExecuteAsync(
            new AssignRoomLandlordCommand(roomId, request.LandlordId),
            cancellationToken);

        return NoContent();
    }
}
