using System.Text;
using Microsoft.Extensions.Options;

namespace RoomToRentRadar.Adapters.Outbound.Authentication;

/// <summary>
/// Validates <see cref="JwtOptions"/> at application startup to prevent running with missing or weak signing keys.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public const int MinimumSecretByteLength = 32; // 256 bits for HMAC-SHA256

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("JWT Issuer ('Jwt:Issuer') must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("JWT Audience ('Jwt:Audience') must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            failures.Add("JWT signing secret ('Jwt:Secret' or environment variable 'JWT__SECRET') is required and must not be empty.");
        }
        else if (Encoding.UTF8.GetByteCount(options.Secret) < MinimumSecretByteLength)
        {
            failures.Add($"JWT signing secret must be at least {MinimumSecretByteLength * 8} bits ({MinimumSecretByteLength} UTF-8 bytes) for HMAC-SHA256.");
        }

        if (options.AccessTokenExpirationMinutes <= 0)
        {
            failures.Add("JWT AccessTokenExpirationMinutes ('Jwt:AccessTokenExpirationMinutes') must be greater than 0.");
        }

        if (options.RefreshTokenExpirationDays <= 0)
        {
            failures.Add("JWT RefreshTokenExpirationDays ('Jwt:RefreshTokenExpirationDays') must be greater than 0.");
        }

        if (options.ClockSkewSeconds < 0)
        {
            failures.Add("JWT ClockSkewSeconds ('Jwt:ClockSkewSeconds') cannot be negative.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
