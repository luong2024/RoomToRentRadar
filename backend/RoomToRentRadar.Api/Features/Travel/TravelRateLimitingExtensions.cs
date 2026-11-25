using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Configuration;
using RoomToRentRadar.Ports.Models.Travel;

namespace RoomToRentRadar.Api.Features.Travel;

public static class TravelRateLimitingExtensions
{
    public static IServiceCollection AddTravelRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.AddPolicy(TravelRateLimitPolicy.Name, context =>
            {
                var travelOptions = context.RequestServices
                    .GetRequiredService<IOptions<TravelOptions>>()
                    .Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, travelOptions.RateLimitPerMinute),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
            rateLimiter.OnRejected = RateLimitResponseWriter.WriteRejectionAsync;
        });

        return services;
    }
}
