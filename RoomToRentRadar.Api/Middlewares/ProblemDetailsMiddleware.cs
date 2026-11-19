// ADR-012, system-architecture 7.2: one global handler turns every exception into problem+json with a stable errorCode.
// Pass-through rules: a started response can no longer be rewritten; a cancelled request means the client disconnected;
// a BadHttpRequestException other than Kestrel's 413 body-too-large keeps its Kestrel status (e.g. malformed body 400)
// and must not regress to 500. Kestrel's 413 bypasses StatusCodeProblemPages, so it is mapped below.
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadar.Api.Middlewares;

public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!ShouldPassThrough(context, ex))
        {
            var mapping = ResolveMapping(ex);
            var isUnexpected = mapping is null;
            mapping ??= ExceptionProblemMap.InternalError;

            if (isUnexpected)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogInformation(
                    "Request {Method} {Path} mapped to {Status} {ErrorCode}: {ExceptionType}",
                    context.Request.Method,
                    context.Request.Path,
                    mapping.Status,
                    mapping.ErrorCode,
                    ex.GetType().Name);
            }

            context.Response.Clear();
            context.Response.StatusCode = mapping.Status;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = mapping.Status,
                Title = mapping.Title,
                Detail = isUnexpected ? "An unexpected error occurred." : ex.Message,
                Instance = context.Request.Path
            };
            problem.Extensions["errorCode"] = mapping.ErrorCode;

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                cancellationToken: context.RequestAborted);
        }
    }

    private static readonly ProblemMapping PayloadTooLarge =
        new(StatusCodes.Status413PayloadTooLarge, "Payload Too Large", ProblemCodes.FileTooLarge);

    private static ProblemMapping? ResolveMapping(Exception ex) =>
        ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
            ? PayloadTooLarge
            : ExceptionProblemMap.Resolve(ex);

    private static bool ShouldPassThrough(HttpContext context, Exception ex) =>
        context.Response.HasStarted
        || (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        || (ex is BadHttpRequestException { StatusCode: not StatusCodes.Status413PayloadTooLarge });
}