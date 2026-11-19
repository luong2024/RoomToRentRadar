// ADR-012, system-architecture 7.2: bodyless 401/403/404/413/415 become problem+json with a stable errorCode.
// 405 and other 4xx intentionally stay bare because no errorCode exists for them.
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadar.Api.Errors;

public static class StatusCodeProblemPages
{
    public static async Task WriteAsync(StatusCodeContext statusCodeContext)
    {
        ArgumentNullException.ThrowIfNull(statusCodeContext);
        var httpContext = statusCodeContext.HttpContext;
        var response = httpContext.Response;

        // Only write when no body exists yet; headers (e.g. WWW-Authenticate from the JWT challenge) are never touched.
        if (response.HasStarted || response.ContentType is not null || response.ContentLength > 0)
        {
            return;
        }

        var mapping = MapStatusCode(response.StatusCode);
        if (mapping is null)
        {
            return;
        }

        response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = response.StatusCode,
            Title = mapping.Value.Title,
            Detail = mapping.Value.Detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["errorCode"] = mapping.Value.ErrorCode;

        await JsonSerializer.SerializeAsync(response.Body, problem, cancellationToken: httpContext.RequestAborted);
    }

    private static (string Title, string Detail, string ErrorCode)? MapStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => ("Unauthorized", "Authentication is required or the credentials are invalid.", ProblemCodes.Unauthorized),
        StatusCodes.Status403Forbidden => ("Forbidden", "You do not have permission to access this resource.", ProblemCodes.Forbidden),
        StatusCodes.Status404NotFound => ("Not Found", "The requested resource was not found.", ProblemCodes.NotFound),
        StatusCodes.Status413PayloadTooLarge => ("Payload Too Large", "The uploaded file is too large.", ProblemCodes.FileTooLarge),
        StatusCodes.Status415UnsupportedMediaType => ("Unsupported Media Type", "The request content type is not supported.", ProblemCodes.UnsupportedMediaType),
        _ => null
    };
}