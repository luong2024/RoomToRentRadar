using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class AuthTokenOptions
{
    public const string SectionName = "Auth:Token";
    public const int MinimumSigningKeyLength = 32;

    private const string DefaultIssuer = "RoomToRentRadar.Api";
    private const string DefaultAudience = "RoomToRentRadar";

    public string Issuer { get; init; } = DefaultIssuer;

    public string Audience { get; init; } = DefaultAudience;

    public string SigningKey { get; init; } = string.Empty;

    public bool IsEphemeralSigningKey { get; init; }

    /// <summary>
    /// Reads <c>Auth:Token</c> settings. Outside development a missing signing key fails fast;
    /// development falls back to a per-process key so local runs work without user-secrets.
    /// </summary>
    public static AuthTokenOptions Resolve(IConfiguration configuration, bool allowEphemeralKey)
    {
        var issuer = configuration[$"{SectionName}:Issuer"]?.Trim();
        var audience = configuration[$"{SectionName}:Audience"]?.Trim();
        var signingKey = configuration[$"{SectionName}:SigningKey"]?.Trim();

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            if (!allowEphemeralKey)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:SigningKey is required. Configure it with "
                    + $"'dotnet user-secrets set \"{SectionName}:SigningKey\" \"<secret>\" --project RoomToRentRadar.Api' "
                    + $"or the Auth__Token__SigningKey environment variable.");
            }

            return new AuthTokenOptions
            {
                Issuer = string.IsNullOrWhiteSpace(issuer) ? DefaultIssuer : issuer,
                Audience = string.IsNullOrWhiteSpace(audience) ? DefaultAudience : audience,
                SigningKey = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(MinimumSigningKeyLength)),
                IsEphemeralSigningKey = true
            };
        }

        if (signingKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"{SectionName}:SigningKey must be at least {MinimumSigningKeyLength} characters for HS256.");
        }

        return new AuthTokenOptions
        {
            Issuer = string.IsNullOrWhiteSpace(issuer) ? DefaultIssuer : issuer,
            Audience = string.IsNullOrWhiteSpace(audience) ? DefaultAudience : audience,
            SigningKey = signingKey
        };
    }
}
