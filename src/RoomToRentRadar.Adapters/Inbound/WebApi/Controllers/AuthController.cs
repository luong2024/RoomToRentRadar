using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Inbound.WebApi.RateLimiting;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingPolicies.Authentication)]
public sealed class AuthController : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly ILoginWithEmailUseCase _loginWithEmailUseCase;
    private readonly ILoginWithGoogleUseCase _loginWithGoogleUseCase;
    private readonly IRefreshSessionUseCase _refreshSessionUseCase;
    private readonly ILogoutUseCase _logoutUseCase;

    public AuthController(
        IRegisterUserUseCase registerUserUseCase,
        ILoginWithEmailUseCase loginWithEmailUseCase,
        ILoginWithGoogleUseCase loginWithGoogleUseCase,
        IRefreshSessionUseCase refreshSessionUseCase,
        ILogoutUseCase logoutUseCase)
    {
        _registerUserUseCase = registerUserUseCase;
        _loginWithEmailUseCase = loginWithEmailUseCase;
        _loginWithGoogleUseCase = loginWithGoogleUseCase;
        _refreshSessionUseCase = refreshSessionUseCase;
        _logoutUseCase = logoutUseCase;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registerUserUseCase.ExecuteAsync(
            new RegisterUserCommand(request.Email, request.Password, request.Role),
            cancellationToken);

        var response = new RegisterUserResponse(
            result.UserId,
            result.Email,
            result.Role,
            result.CreatedAt);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginWithEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _loginWithEmailUseCase.ExecuteAsync(
            new LoginWithEmailCommand(request.Email, request.Password),
            cancellationToken);

        return Ok(MapToSessionResponse(result));
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] LoginWithGoogleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _loginWithGoogleUseCase.ExecuteAsync(
            new LoginWithGoogleCommand(request.IdToken, request.Role),
            cancellationToken);

        return Ok(MapToSessionResponse(result));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _refreshSessionUseCase.ExecuteAsync(
            new RefreshSessionCommand(request.RefreshToken),
            cancellationToken);

        return Ok(MapToSessionResponse(result));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await _logoutUseCase.ExecuteAsync(
            new LogoutCommand(request.RefreshToken),
            cancellationToken);

        return NoContent();
    }

    private static AuthSessionResponse MapToSessionResponse(AuthenticationResult result) =>
        new(
            result.UserId,
            result.Email,
            result.Role,
            result.AccessToken,
            result.AccessTokenExpiresAt,
            result.RefreshToken,
            result.RefreshTokenExpiresAt);
}
