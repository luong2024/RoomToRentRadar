using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/v1/profiles")]
public sealed class ProfileController : ControllerBase
{
    private readonly IGetMyProfileUseCase _getMyProfileUseCase;
    private readonly IUpdateTenantProfileUseCase _updateTenantProfileUseCase;
    private readonly IUpdateLandlordProfileUseCase _updateLandlordProfileUseCase;
    private readonly IVerifyLandlordKycUseCase _verifyLandlordKycUseCase;
    private readonly IGetPublicProfileUseCase _getPublicProfileUseCase;

    public ProfileController(
        IGetMyProfileUseCase getMyProfileUseCase,
        IUpdateTenantProfileUseCase updateTenantProfileUseCase,
        IUpdateLandlordProfileUseCase updateLandlordProfileUseCase,
        IVerifyLandlordKycUseCase verifyLandlordKycUseCase,
        IGetPublicProfileUseCase getPublicProfileUseCase)
    {
        _getMyProfileUseCase = getMyProfileUseCase;
        _updateTenantProfileUseCase = updateTenantProfileUseCase;
        _updateLandlordProfileUseCase = updateLandlordProfileUseCase;
        _verifyLandlordKycUseCase = verifyLandlordKycUseCase;
        _getPublicProfileUseCase = getPublicProfileUseCase;
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await _getMyProfileUseCase.ExecuteAsync(userId, role, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/tenant")]
    [Authorize(Policy = AuthorizationPolicies.StudentOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateTenantProfile(
        [FromBody] UpdateTenantProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var command = new UpdateTenantCommand(
            request.FullName, request.PhoneNumber, request.AvatarUrl, request.Bio,
            request.StudentId, request.University, request.Faculty, request.DateOfBirth,
            request.Gender, request.EmergencyContactName, request.EmergencyContactPhone,
            request.MaxBudget);

        var result = await _updateTenantProfileUseCase.ExecuteAsync(userId, command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/landlord")]
    [Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateLandlordProfile(
        [FromBody] UpdateLandlordProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var command = new UpdateLandlordCommand(
            request.FullName, request.PhoneNumber, request.AvatarUrl, request.Bio,
            request.BusinessName, request.Address, request.CitizenIdNumber,
            request.CitizenIdIssueDate, request.IdentityFrontPhotoUrl,
            request.IdentityBackPhotoUrl, request.BankName, request.BankAccountNumber,
            request.BankAccountHolder);

        var result = await _updateLandlordProfileUseCase.ExecuteAsync(userId, command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("admin/landlords/{targetUserId:guid}/verify")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyLandlord(
        [FromRoute] Guid targetUserId,
        [FromBody] VerifyLandlordKycRequest request,
        CancellationToken cancellationToken)
    {
        var command = new VerifyLandlordKycCommand(targetUserId, request.IsApproved, request.Reason);
        var result = await _verifyLandlordKycUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{targetUserId:guid}/public")]
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [ProducesResponseType(typeof(PublicProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(
        [FromRoute] Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var result = await _getPublicProfileUseCase.ExecuteAsync(targetUserId, cancellationToken);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(claim!);
    }

    private string GetCurrentUserRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? "STUDENT";
}