using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.Auth
{
    /// <summary>
    /// JWT settings for US01 / T01.1, bound from the "Jwt" section of appsettings.
    /// Validated at startup (ValidateOnStart) so a missing or weak SigningKey stops the API immediately
    /// instead of failing on the first login.
    /// The real SigningKey must never be committed: outside Development, set it with the
    /// environment variable Jwt__SigningKey (or user-secrets).
    /// </summary>
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>Who issues the token, e.g. "RoomToRentRadar.Api".</summary>
        [Required(ErrorMessage = "Jwt:Issuer is required.")]
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Who the token is for, e.g. "RoomToRentRadar.Maui".</summary>
        [Required(ErrorMessage = "Jwt:Audience is required.")]
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// HMAC-SHA256 secret. At least 32 characters (256 bits); shorter keys are rejected
        /// because they can be brute-forced and the library refuses keys under 256 bits anyway.
        /// </summary>
        [Required(ErrorMessage = "Jwt:SigningKey is required (set Jwt__SigningKey).")]
        [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters.")]
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>Token lifetime in minutes (agreed: 120).</summary>
        [Range(1, 24 * 60, ErrorMessage = "Jwt:ExpiresMinutes must be between 1 and 1440.")]
        public int ExpiresMinutes { get; set; } = 120;
    }
}
