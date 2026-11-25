using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Auth;

public static class AuthProblemFactory
{
    public const string InvalidLoginRequest = "invalid_login_request";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string InvalidRegistrationRequest = "invalid_registration_request";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string UnsupportedRegistrationRole = "unsupported_registration_role";

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

    public static ProblemDetails InvalidRegistrationRequestProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid registration request",
            Detail = detail
        };
        problem.Extensions["code"] = InvalidRegistrationRequest;
        return problem;
    }

    public static ProblemDetails EmailAlreadyRegisteredProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Status = StatusCodes.Status409Conflict,
            Title = "Email already registered",
            Detail = "An account with this email address already exists."
        };
        problem.Extensions["code"] = EmailAlreadyRegistered;
        return problem;
    }

    public static ProblemDetails UnsupportedRegistrationRoleProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Unsupported registration role",
            Detail = "Only student and landlord accounts can be registered; admin accounts are provisioned by the system administrator."
        };
        problem.Extensions["code"] = UnsupportedRegistrationRole;
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

    public static ProblemDetails ForbiddenProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "The current account does not have permission to access this resource."
        };
        problem.Extensions["code"] = Forbidden;
        return problem;
    }
}
