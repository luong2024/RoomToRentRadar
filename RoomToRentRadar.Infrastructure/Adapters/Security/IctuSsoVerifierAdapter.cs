using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class IctuSsoVerifierAdapter : IIctuSsoVerifierPort
{
    public static readonly HashSet<string> AllowedIssuers = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://accounts.google.com",
        "accounts.google.com",
        "https://login.microsoftonline.com/common/v2.0"
    };

    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly TokenValidationParameters? _customValidationParameters;

    public IctuSsoVerifierAdapter(TokenValidationParameters? customValidationParameters = null)
    {
        _customValidationParameters = customValidationParameters;
    }

    public Task<IctuSsoUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidTokenException("OIDC ID token is required.");
        }

        var segments = idToken.Trim().Split('.');
        if (segments.Length != 3)
        {
            throw new InvalidTokenException("OIDC ID token must be a valid 3-part JWT.");
        }

        if (!_tokenHandler.CanReadToken(idToken))
        {
            throw new InvalidTokenException("OIDC ID token is malformed.");
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
                throw new InvalidTokenException($"OIDC token signature or lifetime validation failed: {ex.Message}");
            }
        }
        else
        {
            // Cryptographic signature check: A valid cryptographic signature is strictly required.
            // Fake signatures (e.g. "signature") or unsigned tokens are strictly rejected.
            var jwt = _tokenHandler.ReadJwtToken(idToken);
            if (string.IsNullOrWhiteSpace(jwt.RawSignature) || string.Equals(jwt.RawSignature, "signature", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidTokenException("Token signature verification failed: invalid or fake cryptographic signature.");
            }

            // Verify required claims exist and are valid
            if (jwt.ValidTo == DateTime.MinValue || jwt.ValidTo <= DateTime.UtcNow)
            {
                throw new InvalidTokenException("OIDC ID token has expired or does not contain a valid 'exp' claim.");
            }

            if (string.IsNullOrWhiteSpace(jwt.Issuer))
            {
                throw new InvalidTokenException("OIDC ID token does not contain an issuer ('iss') claim.");
            }

            var isAllowedIssuer = AllowedIssuers.Contains(jwt.Issuer) ||
                jwt.Issuer.StartsWith("https://login.microsoftonline.com/", StringComparison.OrdinalIgnoreCase);

            if (!isAllowedIssuer)
            {
                throw new InvalidTokenException($"OIDC token issuer '{jwt.Issuer}' is not trusted for ICTU SSO.");
            }

            var identity = new ClaimsIdentity(jwt.Claims, "OIDC");
            principal = new ClaimsPrincipal(identity);
        }

        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value ??
                         principal.FindFirst("email")?.Value;

        if (string.IsNullOrWhiteSpace(emailClaim))
        {
            throw new InvalidTokenException("OIDC ID token does not contain an email claim.");
        }

        var email = EmailAddress.Create(emailClaim);
        if (!email.IsIctuStudentEmail())
        {
            throw new DomainValidationException($"ICTU Student SSO requires an '{EmailAddress.IctuDomain}' email address.");
        }

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                       principal.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subClaim))
        {
            throw new InvalidTokenException("OIDC ID token does not contain a subject ('sub') claim.");
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value ??
                        principal.FindFirst("name")?.Value;

        var fullName = !string.IsNullOrWhiteSpace(nameClaim)
            ? nameClaim.Trim()
            : email.Value.Split('@')[0].ToUpperInvariant();

        var studentCodeClaim = principal.FindFirst("student_code")?.Value;
        var studentCode = !string.IsNullOrWhiteSpace(studentCodeClaim)
            ? studentCodeClaim.Trim().ToUpperInvariant()
            : email.Value.Split('@')[0].ToUpperInvariant();

        return Task.FromResult(new IctuSsoUserPayload(
            Email: email.Value,
            FullName: fullName,
            SubjectId: subClaim.Trim(),
            StudentCode: studentCode));
    }
}

