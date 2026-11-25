using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Shared.Auth;

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
    public const string InvalidOtpRequest = "invalid_otp_request";
    public const string InvalidOtpChallenge = "invalid_otp_challenge";
    public const string InvalidOtp = "invalid_otp";
    public const string OtpAttemptsExceeded = "otp_attempts_exceeded";
    public const string EmailDeliveryFailed = "email_delivery_failed";
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
            $"password must be {AccountRules.PasswordMinLength}-{AccountRules.PasswordMaxLength} characters "
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

    public static ProblemDetails InvalidOtpRequestProblem()
        => RegistrationProblem(
            StatusCodes.Status400BadRequest,
            InvalidOtpRequest,
            "Invalid verification request",
            $"challengeId is required and code must be exactly {AccountRules.OtpLength} digits.");

    public static ProblemDetails InvalidOtpChallengeProblem()
        => RegistrationProblem(
            StatusCodes.Status400BadRequest,
            InvalidOtpChallenge,
            "Invalid verification request",
            "The verification request is unknown or has expired. Start the registration again.");

    public static ProblemDetails ExpiredOtpChallengeProblem()
        => RegistrationProblem(
            StatusCodes.Status400BadRequest,
            InvalidOtpChallenge,
            "Verification code expired",
            "The verification code has expired. Start the registration again.");

    public static ProblemDetails InvalidOtpProblem(int attemptsRemaining)
    {
        var problem = RegistrationProblem(
            StatusCodes.Status400BadRequest,
            InvalidOtp,
            "Incorrect verification code",
            $"The verification code is incorrect. {attemptsRemaining} attempts remaining.");
        problem.Extensions["attempts_remaining"] = attemptsRemaining;
        return problem;
    }

    public static ProblemDetails OtpAttemptsExceededProblem()
        => RegistrationProblem(
            StatusCodes.Status429TooManyRequests,
            OtpAttemptsExceeded,
            "Too many incorrect verification attempts",
            "The verification code was entered incorrectly too many times. Start the registration again.");

    public static ProblemDetails EmailDeliveryFailedProblem()
        => RegistrationProblem(
            StatusCodes.Status502BadGateway,
            EmailDeliveryFailed,
            "Verification email could not be sent",
            "The verification email could not be sent. Try again shortly.");

    private static ProblemDetails RegistrationProblem(int status, string code, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                StatusCodes.Status429TooManyRequests => "https://tools.ietf.org/html/rfc6585#section-4",
                StatusCodes.Status502BadGateway => "https://tools.ietf.org/html/rfc9110#section-15.6.3",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            },
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
            Detail = $"Email and password are required and the password must be at most {AccountRules.PasswordMaxLength} characters."
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
