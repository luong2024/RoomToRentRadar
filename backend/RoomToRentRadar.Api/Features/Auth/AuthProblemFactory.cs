using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public static class AuthProblemFactory
{
    public const string InvalidLoginRequest = "invalid_login_request";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidEmail = "invalid_email";
    public const string WeakPassword = "weak_password";
    public const string InvalidDisplayName = "invalid_display_name";
    public const string InvalidRole = "invalid_role";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string RateLimited = "auth_rate_limited";

    public static ProblemDetails InvalidEmailProblem(string detail)
        => RegistrationProblem(StatusCodes.Status400BadRequest, InvalidEmail, "Invalid email", detail);

    public static ProblemDetails WeakPasswordProblem()
        => RegistrationProblem(
            StatusCodes.Status400BadRequest,
            WeakPassword,
            "Weak password",
            $"password must be {AuthLimits.PasswordMinLength}-{AuthLimits.PasswordMaxLength} characters "
            + "and contain at least one letter and one digit.");

    public static ProblemDetails InvalidDisplayNameProblem(string detail)
        => RegistrationProblem(StatusCodes.Status400BadRequest, InvalidDisplayName, "Invalid display name", detail);

    public static ProblemDetails InvalidRoleProblem()
        => RegistrationProblem(
            StatusCodes.Status400BadRequest,
            InvalidRole,
            "Invalid role",
            "role must be student or landlord; admin accounts are provisioned by the system.");

    public static ProblemDetails EmailAlreadyRegisteredProblem()
        => RegistrationProblem(
            StatusCodes.Status409Conflict,
            EmailAlreadyRegistered,
            "Email already registered",
            "An account with this email address already exists.");

    private static ProblemDetails RegistrationProblem(int status, string code, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = status == StatusCodes.Status409Conflict
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.10"
                : "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    public static ProblemDetails InvalidLoginRequestProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid login request",
            Detail = $"Email and password are required and the password must be at most {AuthLimits.PasswordMaxLength} characters."
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

    public static ProblemDetails RateLimitedProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many authentication requests",
            Detail = "Authentication requests are limited per client. Retry after a short delay."
        };
        problem.Extensions["code"] = RateLimited;
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
