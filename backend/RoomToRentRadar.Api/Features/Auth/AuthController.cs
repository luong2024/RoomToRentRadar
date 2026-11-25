using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!RegistrationValidator.TryCreate(request, out var command, out var validationProblem))
        {
            return ToProblem(validationProblem!);
        }

        var result = await authUseCase.RegisterAsync(command, cancellationToken);
        if (result.Outcome == RegisterUserOutcome.EmailAlreadyRegistered)
        {
            return ToProblem(AuthProblemFactory.EmailAlreadyRegisteredProblem());
        }

        if (result.Outcome == RegisterUserOutcome.RoleNotAllowed)
        {
            return ToProblem(AuthProblemFactory.InvalidRoleProblem());
        }

        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Length > AuthLimits.EmailMaxLength
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length > AuthLimits.PasswordMaxLength)
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
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[bearerPrefix.Length..].Trim();
        }

        await authUseCase.LogoutAsync(token, cancellationToken);
        return NoContent();
    }

    private ObjectResult ToProblem(ProblemDetails problem)
        => Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail,
            type: problem.Type,
            extensions: problem.Extensions);
}
