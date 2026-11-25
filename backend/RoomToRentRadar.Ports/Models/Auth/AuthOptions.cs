namespace RoomToRentRadar.Ports.Models.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int RateLimitPerMinute { get; set; } = 10;
}
