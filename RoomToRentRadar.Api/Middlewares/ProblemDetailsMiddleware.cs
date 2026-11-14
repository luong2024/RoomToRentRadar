// ADR-012, system-architecture 7.2: one global handler turns every exception into problem+json with a stable errorCode.
// Pass-through rules: a started response can no longer be rewritten; a cancelled request means the client disconnected;
// BadHttpRequestException is already translated to 400/413 by Kestrel and must not regress to 500.
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Api.Errors;

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
            var mapping = ExceptionProblemMap.Resolve(ex);
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

    private static bool ShouldPassThrough(HttpContext context, Exception ex) =>
        context.Response.HasStarted
        || (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        || ex is BadHttpRequestException;
}