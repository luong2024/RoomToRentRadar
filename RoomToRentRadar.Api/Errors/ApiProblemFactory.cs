using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadar.Api.Errors;

public static class ApiProblemFactory
{
    public const string ProblemContentType = "application/problem+json";

    public static ProblemDetails Create(
        int statusCode,
        string title,
        string errorCode,
        string detail,
        string? instance = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = instance
        };
        problem.Extensions["errorCode"] = errorCode;
        return problem;
    }

    public static ObjectResult ToResult(ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { ProblemContentType }
        };
    }

    public static ProblemDetails FromModelState(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.Exception?.Message ?? "Invalid value."
                        : error.ErrorMessage)
                    .ToArray());

        var requestTooLarge = IsRequestTooLarge(context.ModelState);

        if (requestTooLarge)
        {
            return Create(
                StatusCodes.Status413PayloadTooLarge,
                "Payload Too Large",
                "FILE_TOO_LARGE",
                $"File exceeds the maximum allowed size of {PhotoFileRules.MaxFileBytes} bytes.",
                context.HttpContext.Request.Path);
        }

        var problem = Create(
            StatusCodes.Status400BadRequest,
            "Validation Error",
            "VALIDATION_FAILED",
            "One or more validation errors occurred.",
            context.HttpContext.Request.Path);
        problem.Extensions["errors"] = errors;
        return problem;
    }

    public static bool IsRequestTooLarge(ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        return modelState.Values
            .SelectMany(entry => entry.Errors)
            .Any(error => IsRequestTooLargeMessage(error.ErrorMessage)
                || IsRequestTooLargeMessage(error.Exception?.Message));
    }

    public static ProblemDetails? FromStatusCode(int statusCode, string? instance) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => Create(
            statusCode,
            "Authentication Failed",
            "INVALID_TOKEN",
            "A valid bearer token is required.",
            instance),
        StatusCodes.Status403Forbidden => Create(
            statusCode,
            "Access Forbidden",
            "FORBIDDEN_ROLE",
            "The authenticated user is not allowed to perform this action.",
            instance),
        StatusCodes.Status413PayloadTooLarge => Create(
            statusCode,
            "Payload Too Large",
            "FILE_TOO_LARGE",
            $"File exceeds the maximum allowed size of {PhotoFileRules.MaxFileBytes} bytes.",
            instance),
        StatusCodes.Status415UnsupportedMediaType => Create(
            statusCode,
            "Unsupported Media Type",
            "UNSUPPORTED_MEDIA_TYPE",
            "Request content type is not supported.",
            instance),
        _ => null
    };

    private static bool IsRequestTooLargeMessage(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && (message.Contains("Request body too large", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Multipart body length limit", StringComparison.OrdinalIgnoreCase));
}
