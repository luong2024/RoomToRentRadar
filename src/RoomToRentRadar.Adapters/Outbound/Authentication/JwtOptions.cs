namespace RoomToRentRadar.Adapters.Outbound.Authentication;

/// <summary>
/// Strongly-typed configuration options for JWT token issuance and validation.
/// Signing secret must be supplied via User Secrets (Development) or environment variables (JWT__SECRET) and never committed to source control.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 symmetric signing key (minimum 256 bits / 32 UTF-8 bytes).
    /// </summary>
    public string Secret { get; init; } = string.Empty;

    public int AccessTokenExpirationMinutes { get; init; } = 15;

    public int RefreshTokenExpirationDays { get; init; } = 7;

    public int ClockSkewSeconds { get; init; } = 30;
}
