using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    AuthService authService,
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

        var response = await authService.LoginAsync(email, password, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ToProblem(AuthProblemFactory.InvalidLoginRequestProblem());
        }

        var response = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
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

        await authService.LogoutAsync(token, cancellationToken);
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
