namespace RoomToRentRadar.Api.Features.Travel;

public sealed class TravelOptions
{
    public const string SectionName = "Travel";

    public string? GoogleRoutesApiKey { get; set; }

    public int CacheMinutes { get; set; } = 10;

    public int FallbackCacheSeconds { get; set; } = 45;

    public int RateLimitPerMinute { get; set; } = 60;
}
