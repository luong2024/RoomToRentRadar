using System.Text.Json;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class IctuSsoVerifierAdapter : IIctuSsoVerifierPort
{
    private static readonly HashSet<string> AllowedIssuers = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://accounts.google.com",
        "accounts.google.com",
        "https://login.microsoftonline.com/common/v2.0"
    };

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

        Dictionary<string, JsonElement>? payload;
        try
        {
            var payloadBytes = JwtTokenServiceAdapter.Base64UrlDecode(segments[1]);
            payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadBytes);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new InvalidTokenException("OIDC ID token payload is malformed.");
        }

        if (payload == null)
        {
            throw new InvalidTokenException("OIDC ID token payload cannot be empty.");
        }

        if (payload.TryGetValue("exp", out var expEl))
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (expEl.ValueKind == JsonValueKind.Number && expEl.GetInt64() <= nowUnix)
            {
                throw new InvalidTokenException("OIDC ID token has expired.");
            }
        }

        if (payload.TryGetValue("iss", out var issEl))
        {
            var issuer = issEl.GetString() ?? string.Empty;
            var isAllowedIssuer = AllowedIssuers.Contains(issuer) ||
                issuer.StartsWith("https://login.microsoftonline.com/", StringComparison.OrdinalIgnoreCase);

            if (!isAllowedIssuer)
            {
                throw new InvalidTokenException($"OIDC token issuer '{issuer}' is not trusted for ICTU SSO.");
            }
        }

        if (!payload.TryGetValue("email", out var emailEl) || string.IsNullOrWhiteSpace(emailEl.GetString()))
        {
            throw new InvalidTokenException("OIDC ID token does not contain an email claim.");
        }

        var email = EmailAddress.Create(emailEl.GetString()!);
        if (!email.IsIctuStudentEmail())
        {
            throw new DomainValidationException($"ICTU Student SSO requires an '{EmailAddress.IctuDomain}' email address.");
        }

        if (!payload.TryGetValue("sub", out var subEl) || string.IsNullOrWhiteSpace(subEl.GetString()))
        {
            throw new InvalidTokenException("OIDC ID token does not contain a subject ('sub') claim.");
        }

        var fullName = payload.TryGetValue("name", out var nameEl) && !string.IsNullOrWhiteSpace(nameEl.GetString())
            ? nameEl.GetString()!.Trim()
            : email.Value.Split('@')[0].ToUpperInvariant();

        var studentCode = payload.TryGetValue("student_code", out var codeEl) && !string.IsNullOrWhiteSpace(codeEl.GetString())
            ? codeEl.GetString()!.Trim().ToUpperInvariant()
            : email.Value.Split('@')[0].ToUpperInvariant();

        return Task.FromResult(new IctuSsoUserPayload(
            Email: email.Value,
            FullName: fullName,
            SubjectId: subEl.GetString()!.Trim(),
            StudentCode: studentCode));
    }
}

