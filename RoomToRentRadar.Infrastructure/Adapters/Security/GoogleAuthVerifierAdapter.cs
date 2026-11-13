using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class GoogleAuthVerifierAdapter : IGoogleAuthVerifierPort
{
    public const string GoogleJwksUrl = "https://www.googleapis.com/oauth2/v3/certs";
    public static readonly HashSet<string> AllowedIssuers = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://accounts.google.com",
        "accounts.google.com"
    };

    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly TokenValidationParameters? _customValidationParameters;
    private readonly HttpClient _httpClient;
    private readonly string? _audience;

    private static ICollection<SecurityKey>? _cachedSigningKeys;
    private static DateTime _keysCachedUntilUtc = DateTime.MinValue;
    private static readonly SemaphoreSlim _keyFetchLock = new(1, 1);

    public GoogleAuthVerifierAdapter(
        TokenValidationParameters? customValidationParameters = null,
        HttpClient? httpClient = null,
        string? audience = null)
    {
        _customValidationParameters = customValidationParameters;
        _httpClient = httpClient ?? new HttpClient();
        _audience = audience
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? Environment.GetEnvironmentVariable("Google__ClientId");
    }

    public static void ResetKeyCache()
    {
        _cachedSigningKeys = null;
        _keysCachedUntilUtc = DateTime.MinValue;
    }

    public async Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidTokenException("Google ID token is required.");
        }

        var segments = idToken.Trim().Split('.');
        if (segments.Length != 3)
        {
            throw new InvalidTokenException("Google ID token must be a valid 3-part JWT.");
        }

        if (!_tokenHandler.CanReadToken(idToken))
        {
            throw new InvalidTokenException("Google ID token is malformed.");
        }

        TokenValidationParameters validationParams;
        if (_customValidationParameters != null)
        {
            validationParams = _customValidationParameters;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_audience))
            {
                throw new InvalidOperationException("Google Client ID (Audience) must be configured for GoogleAuthVerifierAdapter.");
            }

            var signingKeys = await GetGoogleSigningKeysAsync(ct);
            validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidIssuers = AllowedIssuers,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        }

        ClaimsPrincipal principal;
        try
        {
            principal = _tokenHandler.ValidateToken(idToken, validationParams, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new InvalidTokenException($"Google token signature or lifetime validation failed: {ex.Message}");
        }

        var emailVerifiedClaim = principal.FindFirst("email_verified")?.Value;
        if (!string.IsNullOrWhiteSpace(emailVerifiedClaim) &&
            bool.TryParse(emailVerifiedClaim, out var isVerified) && !isVerified)
        {
            throw new InvalidTokenException("Google account email is not verified.");
        }

        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value ??
                         principal.FindFirst("email")?.Value;

        if (string.IsNullOrWhiteSpace(emailClaim))
        {
            throw new InvalidTokenException("Google ID token does not contain an email claim.");
        }

        var email = EmailAddress.Create(emailClaim);

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                       principal.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subClaim))
        {
            throw new InvalidTokenException("Google ID token does not contain a subject ('sub') claim.");
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value ??
                         principal.FindFirst("name")?.Value;

        var fullName = !string.IsNullOrWhiteSpace(nameClaim)
            ? nameClaim.Trim()
            : email.Value.Split('@')[0];

        return new GoogleUserPayload(
            Email: email.Value,
            FullName: fullName,
            SubjectId: subClaim.Trim());
    }

    private async Task<ICollection<SecurityKey>> GetGoogleSigningKeysAsync(CancellationToken ct)
    {
        if (_cachedSigningKeys != null && DateTime.UtcNow < _keysCachedUntilUtc)
        {
            return _cachedSigningKeys;
        }

        await _keyFetchLock.WaitAsync(ct);
        try
        {
            if (_cachedSigningKeys != null && DateTime.UtcNow < _keysCachedUntilUtc)
            {
                return _cachedSigningKeys;
            }

            try
            {
                var response = await _httpClient.GetStringAsync(GoogleJwksUrl, ct);
                var jwks = new JsonWebKeySet(response);
                var keys = jwks.GetSigningKeys();
                if (keys == null || keys.Count == 0)
                {
                    throw new InvalidTokenException("Google JWKS endpoint did not return any signing keys.");
                }

                _cachedSigningKeys = keys;
                _keysCachedUntilUtc = DateTime.UtcNow.AddHours(1);
                return _cachedSigningKeys;
            }
            catch (Exception ex) when (ex is not InvalidTokenException)
            {
                throw new InvalidTokenException($"Failed to retrieve Google signing keys for signature verification: {ex.Message}");
            }
        }
        finally
        {
            _keyFetchLock.Release();
        }
    }
}
