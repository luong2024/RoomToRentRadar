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
    private readonly GoogleAuthVerifierAdapter _googleVerifier;

    public IctuSsoVerifierAdapter(
        TokenValidationParameters? customValidationParameters = null,
        GoogleAuthVerifierAdapter? googleVerifier = null)
    {
        _customValidationParameters = customValidationParameters;
        _googleVerifier = googleVerifier ?? new GoogleAuthVerifierAdapter(customValidationParameters);
    }

    public async Task<IctuSsoUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
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

        if (_customValidationParameters != null)
        {
            ClaimsPrincipal principal;
            try
            {
                principal = _tokenHandler.ValidateToken(idToken, _customValidationParameters, out _);
            }
            catch (SecurityTokenException)
            {
                throw new InvalidTokenException("OIDC token signature or lifetime validation failed.");
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

            return new IctuSsoUserPayload(
                Email: email.Value,
                FullName: fullName,
                SubjectId: subClaim.Trim(),
                StudentCode: studentCode);
        }

        if (!string.IsNullOrWhiteSpace(_googleVerifier.Audience))
        {
            var googlePayload = await _googleVerifier.VerifyIdTokenAsync(idToken, ct);
            var ictuEmail = EmailAddress.Create(googlePayload.Email);
            if (!ictuEmail.IsIctuStudentEmail())
            {
                throw new DomainValidationException($"ICTU Student SSO requires an '{EmailAddress.IctuDomain}' email address.");
            }

            var sCode = ictuEmail.Value.Split('@')[0].ToUpperInvariant();

            return new IctuSsoUserPayload(
                Email: ictuEmail.Value,
                FullName: googlePayload.FullName,
                SubjectId: googlePayload.SubjectId,
                StudentCode: sCode);
        }

        // When no Google audience is configured, unverified/fake signatures are strictly rejected
        throw new InvalidTokenException("Token signature verification failed: unverified or forged cryptographic signature.");
    }
}
