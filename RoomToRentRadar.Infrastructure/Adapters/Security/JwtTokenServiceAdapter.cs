using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "RoomToRentRadar.Api";
    public string Audience { get; set; } = "RoomToRentRadar.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 30;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public sealed class JwtTokenServiceAdapter : IJwtTokenServicePort
{
    private readonly JwtSettings _settings;
    private readonly byte[] _signingKeyBytes;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(Math.Max(1, _settings.RefreshTokenExpirationDays));

    public JwtTokenServiceAdapter(JwtSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException("JWT SigningKey must be provided via configuration and cannot be empty.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(settings.SigningKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");
        }

        _settings = settings;
        _signingKeyBytes = keyBytes;
    }

    public GeneratedAccessToken GenerateAccessToken(User user, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = utcNow.AddMinutes(Math.Max(1, _settings.AccessTokenExpirationMinutes));
        var issuedAtSeconds = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
        var expiresAtSeconds = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds();

        var headerJson = """{"alg":"HS256","typ":"JWT"}""";
        var payloadDict = new Dictionary<string, object>
        {
            ["sub"] = user.Id,
            ["email"] = user.Email.Value,
            ["role"] = user.Role.ToString(),
            [ClaimTypes.Role] = user.Role.ToString(),
            ["is_verified"] = user.IsVerified ? "true" : "false",
            ["jti"] = jwtId,
            ["iss"] = _settings.Issuer,
            ["aud"] = _settings.Audience,
            ["iat"] = issuedAtSeconds,
            ["nbf"] = issuedAtSeconds,
            ["exp"] = expiresAtSeconds
        };

        var payloadJson = JsonSerializer.Serialize(payloadDict);
        var encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signatureBytes = HMACSHA256.HashData(_signingKeyBytes, Encoding.ASCII.GetBytes(signingInput));
        var encodedSignature = Base64UrlEncode(signatureBytes);

        return new GeneratedAccessToken(
            Token: $"{signingInput}.{encodedSignature}",
            JwtId: jwtId,
            ExpiresAtUtc: expiresAtUtc);
    }

    public bool TryValidateAccessToken(
        string token,
        DateTime utcNow,
        out IReadOnlyDictionary<string, JsonElement>? claims)
    {
        claims = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var segments = token.Split('.');
        if (segments.Length != 3)
        {
            return false;
        }

        var signingInput = $"{segments[0]}.{segments[1]}";
        var expectedSignature = HMACSHA256.HashData(_signingKeyBytes, Encoding.ASCII.GetBytes(signingInput));

        byte[] actualSignature;
        byte[] payloadBytes;
        try
        {
            actualSignature = Base64UrlDecode(segments[2]);
            payloadBytes = Base64UrlDecode(segments[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
        {
            return false;
        }

        var parsedClaims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadBytes);
        if (parsedClaims == null)
        {
            return false;
        }

        var nowUnix = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
        if (!parsedClaims.TryGetValue("exp", out var expEl) || expEl.GetInt64() <= nowUnix)
        {
            return false;
        }

        if (parsedClaims.TryGetValue("iss", out var issEl) &&
            !string.Equals(issEl.GetString(), _settings.Issuer, StringComparison.Ordinal))
        {
            return false;
        }

        if (parsedClaims.TryGetValue("aud", out var audEl) &&
            !string.Equals(audEl.GetString(), _settings.Audience, StringComparison.Ordinal))
        {
            return false;
        }

        claims = parsedClaims;
        return true;
    }

    public string GenerateRawRefreshToken()
    {
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Base64UrlEncode(randomBytes);
    }

    public string ComputeTokenHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Raw token cannot be null or whitespace.", nameof(rawToken));
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}

