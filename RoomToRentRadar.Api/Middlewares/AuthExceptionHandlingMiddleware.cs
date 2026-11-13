using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Api.Middlewares;

public sealed class AuthExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public AuthExceptionHandlingMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (TryMapException(ex, out var statusCode, out var title, out var errorCode))
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = ex.Message,
                Instance = context.Request.Path
            };

            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                problem.Extensions["errorCode"] = errorCode;
            }

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                 cancellationToken: context.RequestAborted);
        }
    }

    public static bool TryMapException(Exception exception, out int statusCode, out string title) =>
        TryMapException(exception, out statusCode, out title, out _);

    public static bool TryMapException(Exception exception, out int statusCode, out string title, out string? errorCode)
    {
        switch (exception)
        {
            case DomainValidationException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Validation Error";
                errorCode = "VALIDATION_ERROR";
                return true;

            case PhoneNumberRequiredException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Phone Number Required";
                errorCode = "AUTH_PHONE_REQUIRED";
                return true;

            case HouseNameRequiredException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "House Name Required";
                errorCode = "AUTH_HOUSE_NAME_REQUIRED";
                return true;

            case PasswordRequiredException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Password Required";
                errorCode = "AUTH_PASSWORD_REQUIRED";
                return true;

            case CurrentPasswordRequiredException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Current Password Required";
                errorCode = "AUTH_CURRENT_PASSWORD_REQUIRED";
                return true;

            case InvalidCredentialsException:
                statusCode = StatusCodes.Status401Unauthorized;
                title = "Authentication Failed";
                errorCode = "AUTH_INVALID_CREDENTIALS";
                return true;

            case InvalidTokenException:
                statusCode = StatusCodes.Status401Unauthorized;
                title = "Authentication Failed";
                errorCode = "AUTH_INVALID_TOKEN";
                return true;

            case AccountLockedException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Account Locked";
                errorCode = "AUTH_ACCOUNT_LOCKED";
                return true;

            case ForbiddenRoleException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Access Forbidden";
                errorCode = "AUTH_FORBIDDEN_ROLE";
                return true;

            case LandlordNotApprovedException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Access Forbidden";
                errorCode = "AUTH_LANDLORD_NOT_APPROVED";
                return true;

            case UserNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "User Not Found";
                errorCode = "AUTH_USER_NOT_FOUND";
                return true;

            case DuplicateEmailException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflict";
                errorCode = "AUTH_EMAIL_EXISTS";
                return true;

            case ConcurrencyConflictException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflict";
                errorCode = "CONCURRENCY_CONFLICT";
                return true;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Unexpected Error";
                errorCode = null;
                return false;
        }
    }
}

