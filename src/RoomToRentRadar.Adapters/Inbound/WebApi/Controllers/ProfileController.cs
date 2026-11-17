using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/v1/profiles")]
public sealed class ProfileController : ControllerBase
{
    private readonly IGetMyProfileUseCase _getMyProfileUseCase;
    private readonly IUpdateTenantProfileUseCase _updateTenantProfileUseCase;
    private readonly IUpdateLandlordProfileUseCase _updateLandlordProfileUseCase;
    private readonly IUpdateAdminProfileUseCase _updateAdminProfileUseCase;
    private readonly IVerifyLandlordKycUseCase _verifyLandlordKycUseCase;
    private readonly IGetPublicProfileUseCase _getPublicProfileUseCase;
    private readonly IGetPendingLandlordsUseCase _getPendingLandlordsUseCase;
    private readonly IGetLandlordKycDetailUseCase _getLandlordKycDetailUseCase;
    private readonly ICreateKycUploadUrlUseCase _createKycUploadUrlUseCase;

    public ProfileController(
        IGetMyProfileUseCase getMyProfileUseCase,
        IUpdateTenantProfileUseCase updateTenantProfileUseCase,
        IUpdateLandlordProfileUseCase updateLandlordProfileUseCase,
        IUpdateAdminProfileUseCase updateAdminProfileUseCase,
        IVerifyLandlordKycUseCase verifyLandlordKycUseCase,
        IGetPublicProfileUseCase getPublicProfileUseCase,
        IGetPendingLandlordsUseCase getPendingLandlordsUseCase,
        IGetLandlordKycDetailUseCase getLandlordKycDetailUseCase,
        ICreateKycUploadUrlUseCase createKycUploadUrlUseCase)
    {
        _getMyProfileUseCase = getMyProfileUseCase;
        _updateTenantProfileUseCase = updateTenantProfileUseCase;
        _updateLandlordProfileUseCase = updateLandlordProfileUseCase;
        _updateAdminProfileUseCase = updateAdminProfileUseCase;
        _verifyLandlordKycUseCase = verifyLandlordKycUseCase;
        _getPublicProfileUseCase = getPublicProfileUseCase;
        _getPendingLandlordsUseCase = getPendingLandlordsUseCase;
        _getLandlordKycDetailUseCase = getLandlordKycDetailUseCase;
        _createKycUploadUrlUseCase = createKycUploadUrlUseCase;
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        if (!TryGetCurrentUserRole(out var role, out var roleError))
        {
            return roleError!;
        }

        var result = await _getMyProfileUseCase.ExecuteAsync(userId, role, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/tenant")]
    [Authorize(Policy = AuthorizationPolicies.StudentOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateTenantProfile(
        [FromBody] UpdateTenantProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var command = new UpdateTenantCommand(
            request.FullName, request.PhoneNumber, request.AvatarUrl, request.Bio,
            request.StudentId, request.University, request.Faculty, request.DateOfBirth,
            request.Gender, request.EmergencyContactName, request.EmergencyContactPhone,
            request.MaxBudget);

        var result = await _updateTenantProfileUseCase.ExecuteAsync(userId, command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("me/landlord/kyc-upload-url")]
    [Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
    [ProducesResponseType(typeof(KycUploadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateKycUploadUrl(
        [FromBody] CreateKycUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var result = await _createKycUploadUrlUseCase.ExecuteAsync(
            userId,
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("me/landlord")]
    [Authorize(Policy = AuthorizationPolicies.LandlordOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateLandlordProfile(
        [FromBody] UpdateLandlordProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var command = new UpdateLandlordCommand(
            request.FullName, request.PhoneNumber, request.AvatarUrl, request.Bio,
            request.BusinessName, request.Address, request.CitizenIdNumber,
            request.CitizenIdIssueDate, request.IdentityFrontBlobName,
            request.IdentityBackBlobName, request.BankName, request.BankAccountNumber,
            request.BankAccountHolder);

        var result = await _updateLandlordProfileUseCase.ExecuteAsync(userId, command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAdminProfile(
        [FromBody] UpdateAdminProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var authError))
        {
            return authError!;
        }

        var command = new UpdateAdminCommand(
            request.FullName,
            request.PhoneNumber,
            request.StaffCode,
            request.Department,
            request.Title,
            request.AvatarUrl,
            request.Bio);

        var result = await _updateAdminProfileUseCase.ExecuteAsync(userId, command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("admin/landlords/pending")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<AdminLandlordKycDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingLandlords(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take < 1 || take > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Pagination Parameters",
                Detail = "skip must be >= 0 and take must be between 1 and 100.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _getPendingLandlordsUseCase.ExecuteAsync(skip, take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("admin/landlords/{targetUserId:guid}/kyc")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(AdminLandlordKycDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLandlordKycDetail(
        [FromRoute] Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var result = await _getLandlordKycDetailUseCase.ExecuteAsync(targetUserId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("admin/landlords/{targetUserId:guid}/verify")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(
        [FromRoute] Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var result = await _getPublicProfileUseCase.ExecuteAsync(targetUserId, cancellationToken);
        return Ok(result);
    }

    private bool TryGetCurrentUserId(out Guid userId, out IActionResult? failureResult)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out userId) || userId == Guid.Empty)
        {
            userId = Guid.Empty;
            failureResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "A valid user identifier (GUID) is missing or malformed in the security token.",
                Instance = HttpContext.Request.Path
            });
            return false;
        }

        failureResult = null;
        return true;
    }

    private bool TryGetCurrentUserRole(out string role, out IActionResult? failureResult)
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value;

        if (string.IsNullOrWhiteSpace(roleClaim))
        {
            role = string.Empty;
            failureResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "A valid user role claim is missing from the security token.",
                Instance = HttpContext.Request.Path
            });
            return false;
        }

        try
        {
            var parsedRole = UserRoleExtensions.ParseRole(roleClaim);
            role = parsedRole.ToRoleName();
            failureResult = null;
            return true;
        }
        catch (InvalidRoleException)
        {
            role = string.Empty;
            failureResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = $"The role claim '{roleClaim}' in the security token is invalid.",
                Instance = HttpContext.Request.Path
            });
            return false;
        }
    }
}
