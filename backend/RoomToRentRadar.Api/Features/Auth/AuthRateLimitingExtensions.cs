using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Configuration;
using RoomToRentRadar.Ports.Models.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public static class AuthRateLimitingExtensions
{
    public static IServiceCollection AddAuthRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.AddPolicy(AuthRateLimitPolicy.Name, context =>
            {
                var authOptions = context.RequestServices
                    .GetRequiredService<IOptions<AuthOptions>>()
                    .Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, authOptions.RateLimitPerMinute),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
            rateLimiter.OnRejected = RateLimitResponseWriter.WriteRejectionAsync;
        });

        return services;
    }
}
