using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Api.Middlewares;

public sealed class AuditLoggingMiddleware
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogRepository auditLogRepository)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(auditLogRepository);

        if (!MutatingMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Exception? caughtException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caughtException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await RecordAuditLogAsync(context, auditLogRepository, stopwatch.ElapsedMilliseconds, caughtException);
        }
    }

    private static async Task RecordAuditLogAsync(
        HttpContext context,
        IAuditLogRepository auditLogRepository,
        long durationMs,
        Exception? caughtException)
    {
        try
        {
            var request = context.Request;
            var path = request.Path.Value ?? "/";
            var isAuthEndpoint = path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase);

            var userId = context.User?.FindFirst("sub")?.Value
                ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = context.User?.FindFirst("email")?.Value
                ?? context.User?.FindFirst(ClaimTypes.Email)?.Value;
            var role = context.User?.FindFirst("role")?.Value
                ?? context.User?.FindFirst(ClaimTypes.Role)?.Value
                ?? "Anonymous";

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                userAgent = null;
            }

            var statusCode = context.Response.StatusCode;
            var isSuccess = caughtException == null && statusCode >= 200 && statusCode < 400;

            var (entityType, entityId) = ParseRoute(path);

            string? detailsJson = null;
            if (!isAuthEndpoint && request.QueryString.HasValue)
            {
                detailsJson = request.QueryString.Value;
            }

            var entry = new AuditLogEntry(
                id: null,
                userId: userId,
                userEmail: userEmail,
                role: role,
                action: request.Method,
                entityType: entityType,
                entityId: entityId,
                detailsJson: detailsJson,
                ipAddress: ipAddress,
                userAgent: userAgent,
                statusCode: statusCode,
                durationMs: durationMs,
                isSuccess: isSuccess,
                errorMessage: caughtException?.Message);

            await auditLogRepository.InsertAsync(entry, context.RequestAborted);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AUDIT_LOG_MIDDLEWARE_ERROR] Failed to record audit log: {ex.Message}");
        }
    }

    private static (string EntityType, string? EntityId) ParseRoute(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return ("Api", null);
        }

        var typeIndex = string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) && segments.Length > 1
            ? 1
            : 0;

        var entityType = segments[typeIndex];
        string? entityId = segments.Length > typeIndex + 1 ? segments[typeIndex + 1] : null;

        return (entityType, entityId);
    }
}
