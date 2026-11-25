using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.SqliteDataAccess.Seed;

namespace RoomToRentRadar.Api.Features.Auth;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthUseCase authUseCase,
    IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("demo-login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoginResponse>> DemoLogin(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var email = configuration[DemoUserSeeder.EmailKey]?.Trim();
        var password = configuration[DemoUserSeeder.PasswordKey];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return NotFound();
        }

        var response = await authUseCase.LoginAsync(email, password, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicy.Name)]
    [ProducesResponseType(typeof(RegisterChallengeResponse), StatusCodes.Status202Accepted, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<ActionResult<RegisterChallengeResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!RegistrationValidator.TryCreate(request, out var command, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        var result = await authUseCase.BeginRegistrationAsync(command, cancellationToken);
        return result.Outcome switch
        {
            BeginRegistrationOutcome.EmailAlreadyRegistered
                => ToProblem(AuthProblemFactory.EmailAlreadyRegisteredProblem()),
            BeginRegistrationOutcome.RoleNotAllowed
                => ToProblem(AuthProblemFactory.InvalidRoleProblem()),
            BeginRegistrationOutcome.EmailDeliveryFailed
                => ToProblem(AuthProblemFactory.EmailDeliveryFailedProblem()),
            _ => Accepted(result.Response)
        };
    }

    [HttpPost("register/verify")]
    [EnableRateLimiting(AuthRateLimitPolicy.VerifyName)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> VerifyRegistration(
        [FromBody] VerifyRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!RegistrationValidator.TryCreate(request, out var command, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        var result = await authUseCase.VerifyRegistrationAsync(command, cancellationToken);
        if (result.Outcome == VerifyRegistrationOutcome.Verified)
        {
            return Ok(result.Response);
        }

        var problem = result.Outcome switch
        {
            VerifyRegistrationOutcome.ChallengeExpired => AuthProblemFactory.ExpiredOtpChallengeProblem(),
            VerifyRegistrationOutcome.InvalidCode => AuthProblemFactory.InvalidOtpProblem(result.AttemptsRemaining),
            VerifyRegistrationOutcome.AttemptsExceeded => AuthProblemFactory.OtpAttemptsExceededProblem(),
            VerifyRegistrationOutcome.EmailAlreadyRegistered => AuthProblemFactory.EmailAlreadyRegisteredProblem(),
            _ => AuthProblemFactory.InvalidOtpChallengeProblem()
        };
        return ToProblem(problem);
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicy.Name)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Length > AccountRules.EmailMaxLength
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length > AccountRules.PasswordMaxLength)
        {
            return ToProblem(AuthProblemFactory.InvalidLoginRequestProblem());
        }

        var response = await authUseCase.LoginAsync(request.Email, request.Password, cancellationToken);
        if (response is null)
        {
            return ToProblem(AuthProblemFactory.InvalidCredentialsProblem());
        }

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await authUseCase.GetProfileAsync(CurrentUserId, cancellationToken);
        return profile is null
            ? ToProblem(AuthProblemFactory.ProfileNotFoundProblem())
            : Ok(profile);
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (!AccountRules.IsDisplayNameValid(displayName))
        {
            return ToProblem(AuthProblemFactory.InvalidDisplayNameProblem(
                $"display_name is required and must contain at most {AccountRules.DisplayNameMaxLength} characters."));
        }

        var profile = await authUseCase.UpdateDisplayNameAsync(
            CurrentUserId,
            displayName,
            cancellationToken);
        return profile is null
            ? ToProblem(AuthProblemFactory.ProfileNotFoundProblem())
            : Ok(profile);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            await authUseCase.LogoutAsync(token, cancellationToken);
        }

        return NoContent();
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
