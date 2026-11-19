using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Middleware;

/// <summary>
/// Maps known Domain and Application exceptions to RFC 7807 ProblemDetails HTTP responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, extensions) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled server error occurred for {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (extensions is not null)
        {
            foreach (var kvp in extensions)
            {
                problemDetails.Extensions[kvp.Key] = kvp.Value;
            }
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title, string Detail, IDictionary<string, object?>? Extensions) MapException(
        Exception exception)
    {
        return exception switch
        {
            RequestValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                validationEx.Message,
                new Dictionary<string, object?> { ["errors"] = validationEx.Errors }),

            ValidationException queryValidationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                queryValidationEx.Message,
                new Dictionary<string, object?> { ["errors"] = queryValidationEx.Errors }),

            InvalidEmailException emailEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Email Address",
                emailEx.Message,
                null),

            InvalidRoleException roleEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Role",
                roleEx.Message,
                null),

            AdminSelfRegistrationForbiddenException adminEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Registration Role",
                adminEx.Message,
                null),

            InvalidUserStateException userStateEx => (
                StatusCodes.Status400BadRequest,
                "Invalid User State",
                userStateEx.Message,
                null),

            DuplicateEmailException duplicateEx => (
                StatusCodes.Status409Conflict,
                "Email Conflict",
                duplicateEx.Message,
                null),

            ExternalIdentityConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                "Identity Conflict",
                conflictEx.Message,
                null),

            ConcurrencyConflictException concurrencyEx => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                concurrencyEx.Message,
                null),

            AuthenticationFailedException authEx => (
                StatusCodes.Status401Unauthorized,
                "Authentication Failed",
                authEx.Message,
                null),

            InvalidGoogleTokenException googleEx => (
                StatusCodes.Status401Unauthorized,
                "Invalid Google Identity Token",
                googleEx.Message,
                null),

            InvalidRefreshTokenException refreshEx => (
                StatusCodes.Status401Unauthorized,
                "Invalid Refresh Token",
                refreshEx.Message,
                null),

            InvalidRefreshTokenStateException refreshStateEx => (
                StatusCodes.Status401Unauthorized,
                "Invalid Refresh Token State",
                refreshStateEx.Message,
                null),

            UserInactiveException inactiveEx => (
                StatusCodes.Status401Unauthorized,
                "Account Inactive",
                inactiveEx.Message,
                null),

            RouteNotFoundException routeNotFoundEx => (
                StatusCodes.Status404NotFound,
                "Route Not Found",
                routeNotFoundEx.Message,
                null),

            RoutingProviderUnavailableException routingUnavailableEx => (
                StatusCodes.Status502BadGateway,
                "Routing Provider Unavailable",
                routingUnavailableEx.Message,
                null),

            RoutingProviderTimeoutException routingTimeoutEx => (
                StatusCodes.Status504GatewayTimeout,
                "Routing Provider Timeout",
                routingTimeoutEx.Message,
                null),

            NotFoundException resourceNotFoundEx => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                resourceNotFoundEx.Message,
                null),

            ProfileNotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Profile Not Found",
                notFoundEx.Message,
                null),

            ProfileRoleMismatchException roleMismatchEx => (
                StatusCodes.Status403Forbidden,
                "Profile Role Mismatch",
                roleMismatchEx.Message,
                null),

            ProfileDomainException profileDomainEx => (
                StatusCodes.Status400BadRequest,
                "Profile Validation Error",
                profileDomainEx.Message,
                null),

            RateLimitExceededException rateLimitEx => (
                StatusCodes.Status429TooManyRequests,
                "Rate Limit Exceeded",
                rateLimitEx.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing the request.",
                null)
        };
    }
}
