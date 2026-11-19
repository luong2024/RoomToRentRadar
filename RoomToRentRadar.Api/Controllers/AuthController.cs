using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly ILoginUserUseCase _loginUserUseCase;
    private readonly ILoginWithIctuSsoUseCase _loginWithIctuSsoUseCase;
    private readonly IRefreshTokenUseCase _refreshTokenUseCase;
    private readonly IRevokeTokenUseCase _revokeTokenUseCase;
    private readonly IGetCurrentUserProfileUseCase _getCurrentUserProfileUseCase;
    private readonly IApproveLandlordUseCase _approveLandlordUseCase;

    public AuthController(
        IRegisterUserUseCase registerUserUseCase,
        ILoginUserUseCase loginUserUseCase,
        ILoginWithIctuSsoUseCase loginWithIctuSsoUseCase,
        IRefreshTokenUseCase refreshTokenUseCase,
        IRevokeTokenUseCase revokeTokenUseCase,
        IGetCurrentUserProfileUseCase getCurrentUserProfileUseCase,
        IApproveLandlordUseCase approveLandlordUseCase)
    {
        _registerUserUseCase = registerUserUseCase;
        _loginUserUseCase = loginUserUseCase;
        _loginWithIctuSsoUseCase = loginWithIctuSsoUseCase;
        _refreshTokenUseCase = refreshTokenUseCase;
        _revokeTokenUseCase = revokeTokenUseCase;
        _getCurrentUserProfileUseCase = getCurrentUserProfileUseCase;
        _approveLandlordUseCase = approveLandlordUseCase;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthTokensDto>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken ct)
    {
        var result = await _registerUserUseCase.ExecuteAsync(request, ct);
        return CreatedAtAction(nameof(GetMe), new { }, result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthTokensDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _loginUserUseCase.ExecuteAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("sso/ictu")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensDto>> LoginWithIctuSso(
        [FromBody] IctuSsoLoginRequest request,
        CancellationToken ct)
    {
        var result = await _loginWithIctuSsoUseCase.ExecuteAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensDto>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        var result = await _refreshTokenUseCase.ExecuteAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeTokenRequest request,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _revokeTokenUseCase.ExecuteAsync(userId, request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var profile = await _getCurrentUserProfileUseCase.ExecuteAsync(userId, ct);
        return Ok(profile);
    }

    [HttpPatch("admin/landlords/{userId}/approve")]
    [Authorize(Policy = AuthorizationPolicies.AdminPolicy)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> ApproveLandlord(
        [FromRoute] string userId,
        CancellationToken ct)
    {
        var adminUserId = User.GetRequiredUserId();
        var profile = await _approveLandlordUseCase.ExecuteAsync(
            adminUserId,
            new ApproveLandlordRequest(userId),
            ct);
        return Ok(profile);
    }
}

