using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.Options;

/// <summary>
/// JWT settings bound from the "Jwt" section of appsettings.json (US01 / T01.1).
/// Validated at startup (see Program.cs) so a missing or too-short key fails fast
/// instead of producing tokens that cannot be verified.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Who issues the token (this API), checked by JwtBearer on every request.</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Who the token is for (the MAUI client), checked by JwtBearer on every request.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 secret. Must be at least 32 characters (256 bits).
    /// The value in appsettings.json is a DEV-ONLY placeholder: replace it with a real secret
    /// (user-secrets or environment variable Jwt__SigningKey) before deploying.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token lifetime in minutes.</summary>
    [Range(1, 7 * 24 * 60)]
    public int ExpiresMinutes { get; set; } = 120;
}
