using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Auth;

public static class AuthProblemFactory
{
    public const string InvalidLoginRequest = "invalid_login_request";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Unauthorized = "unauthorized";

    public static ProblemDetails InvalidLoginRequestProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid login request",
            Detail = "Email and password are required."
        };
        problem.Extensions["code"] = InvalidLoginRequest;
        return problem;
    }

    public static ProblemDetails InvalidCredentialsProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status401Unauthorized,
            Title = "Invalid credentials",
            Detail = "The email or password is incorrect."
        };
        problem.Extensions["code"] = InvalidCredentials;
        return problem;
    }

    public static ProblemDetails UnauthorizedProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication required",
            Detail = "A valid bearer token is required to access this resource."
        };
        problem.Extensions["code"] = Unauthorized;
        return problem;
    }
}
