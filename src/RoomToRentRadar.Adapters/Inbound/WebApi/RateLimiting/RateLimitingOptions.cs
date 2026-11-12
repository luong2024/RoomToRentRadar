namespace RoomToRentRadar.Adapters.Inbound.WebApi.RateLimiting;

public static class RateLimitingPolicies
{
    public const string Authentication = "AuthenticationRateLimit";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; init; } = 30;

    public int AuthWindowSeconds { get; init; } = 60;
}
