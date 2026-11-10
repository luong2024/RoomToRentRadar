using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Api.Middlewares;

public sealed class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public Task InvokeAsync(HttpContext context, IAuditLogRepository auditLogRepository)
    {
        throw new NotImplementedException();
    }
}

