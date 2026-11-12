using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class MockGoogleAuthVerifierAdapter : IGoogleAuthVerifierPort
{
    public const string MockPrefix = "mock-google:";
    private readonly GoogleAuthVerifierAdapter _googleVerifier = new();

    public Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidTokenException("Google ID token is required.");
        }

        var trimmed = idToken.Trim();

        // 1. Format: mock-google:<email>:<fullName>
        if (trimmed.StartsWith(MockPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed[MockPrefix.Length..].Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new InvalidTokenException("Mock Google token must follow format 'mock-google:<email>:<fullName>'.");
            }

            var email = EmailAddress.Create(parts[0]);
            var fullName = parts[1];

            return Task.FromResult(new GoogleUserPayload(
                Email: email.Value,
                FullName: fullName,
                SubjectId: $"mock-google-sub-{email.Value}"));
        }

        // 2. Convenience tokens for simple tests / UI demo
        if (string.Equals(trimmed, "mock-google-student-token", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "mock-google-id-token", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new GoogleUserPayload(
                Email: "student.radar@ictu.edu.vn",
                FullName: "ICTU Demo Student",
                SubjectId: "mock-google-sub-student-01"));
        }

        if (string.Equals(trimmed, "mock-google-landlord-token", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new GoogleUserPayload(
                Email: "landlord.radar@gmail.com",
                FullName: "Demo Landlord",
                SubjectId: "mock-google-sub-landlord-01"));
        }

        return _googleVerifier.VerifyIdTokenAsync(trimmed, ct);
    }
}

