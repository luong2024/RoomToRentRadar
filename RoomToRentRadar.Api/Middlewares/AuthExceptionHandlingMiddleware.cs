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
        catch (Exception ex) when (TryMapException(ex, out var statusCode, out var title))
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

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                 cancellationToken: context.RequestAborted);
        }
    }

    public static bool TryMapException(Exception exception, out int statusCode, out string title)
    {
        switch (exception)
        {
            case DomainValidationException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Validation Error";
                return true;

            case InvalidCredentialsException:
            case InvalidTokenException:
                statusCode = StatusCodes.Status401Unauthorized;
                title = "Authentication Failed";
                return true;

            case AccountLockedException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Account Locked";
                return true;

            case ForbiddenRoleException:
            case LandlordNotApprovedException:
                statusCode = StatusCodes.Status403Forbidden;
                title = "Access Forbidden";
                return true;

            case UserNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "User Not Found";
                return true;

            case DuplicateEmailException:
            case ConcurrencyConflictException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflict";
                return true;

            case FileTooLargeException:
                statusCode = StatusCodes.Status413PayloadTooLarge;
                title = "Payload Too Large";
                return true;

            case UnsupportedMediaTypeException:
                statusCode = StatusCodes.Status415UnsupportedMediaType;
                title = "Unsupported Media Type";
                return true;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Unexpected Error";
                return false;
        }
    }
}

