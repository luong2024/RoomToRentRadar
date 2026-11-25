using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Travel;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Configuration;

/// <summary>
/// Writes the 429 problem for any rate-limited endpoint. <c>AddRateLimiter</c> keeps a single
/// rejection handler, so the handler resolves the problem from the endpoint's policy name.
/// </summary>
public static class RateLimitResponseWriter
{
    public static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var policyName = context.HttpContext.GetEndpoint()?.Metadata
            .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var problem = policyName == AuthRateLimitPolicy.Name
            ? AuthProblemFactory.RateLimitedProblem()
            : TravelProblemFactory.RateLimitedProblem();

        context.HttpContext.Response.StatusCode = problem.Status!.Value;
        await context.HttpContext.Response.WriteAsJsonAsync(
            problem,
            ApiJson.Options,
            "application/problem+json",
            cancellationToken);
    }
}
