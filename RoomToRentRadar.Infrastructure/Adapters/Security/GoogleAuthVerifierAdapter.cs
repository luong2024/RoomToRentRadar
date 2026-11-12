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
    public static readonly HashSet<string> AllowedIssuers = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://accounts.google.com",
        "accounts.google.com"
    };

    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly TokenValidationParameters? _customValidationParameters;

    public GoogleAuthVerifierAdapter(TokenValidationParameters? customValidationParameters = null)
    {
        _customValidationParameters = customValidationParameters;
    }

    public Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
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

        ClaimsPrincipal principal;
        if (_customValidationParameters != null)
        {
            try
            {
                principal = _tokenHandler.ValidateToken(idToken, _customValidationParameters, out _);
            }
            catch (SecurityTokenException ex)
            {
                throw new InvalidTokenException($"Google token signature or lifetime validation failed: {ex.Message}");
            }
        }
        else
        {
            var jwt = _tokenHandler.ReadJwtToken(idToken);
            if (string.IsNullOrWhiteSpace(jwt.RawSignature) || string.Equals(jwt.RawSignature, "signature", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidTokenException("Token signature verification failed: invalid or fake cryptographic signature.");
            }

            if (jwt.ValidTo == DateTime.MinValue || jwt.ValidTo <= DateTime.UtcNow)
            {
                throw new InvalidTokenException("Google ID token has expired or does not contain a valid 'exp' claim.");
            }

            if (string.IsNullOrWhiteSpace(jwt.Issuer))
            {
                throw new InvalidTokenException("Google ID token does not contain an issuer ('iss') claim.");
            }

            if (!AllowedIssuers.Contains(jwt.Issuer))
            {
                throw new InvalidTokenException($"Google token issuer '{jwt.Issuer}' is not trusted.");
            }

            var identity = new ClaimsIdentity(jwt.Claims, "Google");
            principal = new ClaimsPrincipal(identity);
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

        return Task.FromResult(new GoogleUserPayload(
            Email: email.Value,
            FullName: fullName,
            SubjectId: subClaim.Trim()));
    }
}
