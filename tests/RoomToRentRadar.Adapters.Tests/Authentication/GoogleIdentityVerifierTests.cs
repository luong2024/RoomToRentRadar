using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Application.Exceptions;

namespace RoomToRentRadar.Adapters.Tests.Authentication;

public class GoogleIdentityVerifierTests
{
    private const string ValidClientId = "test-google-client-id.apps.googleusercontent.com";

    private RSA _rsa = null!;
    private RsaSecurityKey _signingKey = null!;
    private GoogleIdentityVerifier _verifier = null!;

    [SetUp]
    public void SetUp()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "google-key-1" };

        var openIdConfig = new OpenIdConnectConfiguration
        {
            Issuer = "https://accounts.google.com"
        };
        openIdConfig.SigningKeys.Add(_signingKey);

        var options = Options.Create(new GoogleAuthOptions { ClientId = ValidClientId });
        _verifier = new GoogleIdentityVerifier(options, new StaticOpenIdConfigManager(openIdConfig));
    }

    [TearDown]
    public void TearDown()
    {
        _rsa.Dispose();
    }

    [Test]
    public async Task VerifyIdTokenAsync_WhenSignedTokenValidWithVerifiedEmail_ShouldReturnIdentity()
    {
        var token = CreateGoogleIdToken(
            issuer: "https://accounts.google.com",
            audience: ValidClientId,
            subject: "google-sub-777",
            email: "verified.user@gmail.com",
            emailVerified: "true",
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            signingKey: _signingKey);

        var identity = await _verifier.VerifyIdTokenAsync(token);

        Assert.Multiple(() =>
        {
            Assert.That(identity.Subject, Is.EqualTo("google-sub-777"));
            Assert.That(identity.Email, Is.EqualTo("verified.user@gmail.com"));
            Assert.That(identity.EmailVerified, Is.True);
        });
    }

    [Test]
    public void VerifyIdTokenAsync_WhenSignedWithUntrustedKey_ShouldThrowInvalidGoogleTokenException()
    {
        using var untrustedRsa = RSA.Create(2048);
        var untrustedKey = new RsaSecurityKey(untrustedRsa) { KeyId = "attacker-key" };

        var token = CreateGoogleIdToken(
            issuer: "https://accounts.google.com",
            audience: ValidClientId,
            subject: "google-sub-777",
            email: "user@gmail.com",
            emailVerified: "true",
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            signingKey: untrustedKey);

        Assert.ThrowsAsync<InvalidGoogleTokenException>(async () =>
            await _verifier.VerifyIdTokenAsync(token));
    }

    [Test]
    public void VerifyIdTokenAsync_WhenAudienceMismatched_ShouldThrowInvalidGoogleTokenException()
    {
        var token = CreateGoogleIdToken(
            issuer: "https://accounts.google.com",
            audience: "wrong-client-id.apps.googleusercontent.com",
            subject: "google-sub-777",
            email: "user@gmail.com",
            emailVerified: "true",
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            signingKey: _signingKey);

        Assert.ThrowsAsync<InvalidGoogleTokenException>(async () =>
            await _verifier.VerifyIdTokenAsync(token));
    }

    [Test]
    public void VerifyIdTokenAsync_WhenIssuerInvalid_ShouldThrowInvalidGoogleTokenException()
    {
        var token = CreateGoogleIdToken(
            issuer: "https://evil-issuer.example.com",
            audience: ValidClientId,
            subject: "google-sub-777",
            email: "user@gmail.com",
            emailVerified: "true",
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            signingKey: _signingKey);

        Assert.ThrowsAsync<InvalidGoogleTokenException>(async () =>
            await _verifier.VerifyIdTokenAsync(token));
    }

    [Test]
    public void VerifyIdTokenAsync_WhenTokenExpired_ShouldThrowInvalidGoogleTokenException()
    {
        var token = CreateGoogleIdToken(
            issuer: "https://accounts.google.com",
            audience: ValidClientId,
            subject: "google-sub-777",
            email: "user@gmail.com",
            emailVerified: "true",
            expiresUtc: DateTime.UtcNow.AddMinutes(-5),
            signingKey: _signingKey);

        Assert.ThrowsAsync<InvalidGoogleTokenException>(async () =>
            await _verifier.VerifyIdTokenAsync(token));
    }

    [Test]
    public void VerifyIdTokenAsync_WhenEmailNotVerified_ShouldThrowInvalidGoogleTokenException()
    {
        var token = CreateGoogleIdToken(
            issuer: "https://accounts.google.com",
            audience: ValidClientId,
            subject: "google-sub-777",
            email: "unverified@gmail.com",
            emailVerified: "false",
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            signingKey: _signingKey);

        Assert.ThrowsAsync<InvalidGoogleTokenException>(async () =>
            await _verifier.VerifyIdTokenAsync(token));
    }

    private static string CreateGoogleIdToken(
        string issuer,
        string audience,
        string subject,
        string email,
        string emailVerified,
        DateTime expiresUtc,
        SecurityKey signingKey)
    {
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresUtc.AddMinutes(-10),
            NotBefore = expiresUtc.AddMinutes(-10),
            Expires = expiresUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("email_verified", emailVerified)
            ]),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };

        return handler.CreateToken(descriptor);
    }

    private sealed class StaticOpenIdConfigManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly OpenIdConnectConfiguration _config;

        public StaticOpenIdConfigManager(OpenIdConnectConfiguration config)
        {
            _config = config;
        }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            Task.FromResult(_config);

        public void RequestRefresh()
        {
        }
    }
}
