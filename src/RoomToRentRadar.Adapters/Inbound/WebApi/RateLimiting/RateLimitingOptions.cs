namespace RoomToRentRadar.Adapters.Inbound.WebApi.RateLimiting;

public static class RateLimitingPolicies
{
    public const string Authentication = "AuthenticationRateLimit";
    public const string Routing = "RoutingRateLimit";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; init; } = 30;

    public int AuthWindowSeconds { get; init; } = 60;

    public int RoutingPermitLimit { get; init; } = 60;

    public int RoutingWindowSeconds { get; init; } = 60;
}
