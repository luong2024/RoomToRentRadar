using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

/// <summary>
/// Deterministic ICTU SSO verifier for Dev/Test environments.
/// Accepts either:
/// 1) Mock token format: "mock-ictu:&lt;email&gt;:&lt;fullName&gt;:&lt;studentCode&gt;"
/// 2) Standard 3-part OIDC JWT format delegated to <see cref="IctuSsoVerifierAdapter"/>.
/// </summary>
public sealed class MockIctuSsoVerifierAdapter : IIctuSsoVerifierPort
{
    public const string MockPrefix = "mock-ictu:";
    private readonly IctuSsoVerifierAdapter _oidcVerifier = new();

    public Task<IctuSsoUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidTokenException("ICTU SSO ID token is required.");
        }

        var trimmed = idToken.Trim();
        if (trimmed.StartsWith(MockPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed[MockPrefix.Length..].Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new InvalidTokenException("Mock ICTU SSO token must follow format 'mock-ictu:<email>:<fullName>[:<studentCode>]'.");
            }

            var email = EmailAddress.Create(parts[0]);
            if (!email.IsIctuStudentEmail())
            {
                throw new DomainValidationException($"ICTU Student SSO requires an '{EmailAddress.IctuDomain}' email address.");
            }

            var fullName = parts[1];
            var studentCode = parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])
                ? parts[2].ToUpperInvariant()
                : email.Value.Split('@')[0].ToUpperInvariant();

            return Task.FromResult(new IctuSsoUserPayload(
                Email: email.Value,
                FullName: fullName,
                SubjectId: $"mock-sub-{email.Value}",
                StudentCode: studentCode));
        }

        return _oidcVerifier.VerifyIdTokenAsync(trimmed, ct);
    }
}

