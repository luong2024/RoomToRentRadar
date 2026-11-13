using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Security;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class MockGoogleAuthVerifierAdapterTests
{
    private MockGoogleAuthVerifierAdapter _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new MockGoogleAuthVerifierAdapter();
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithMockFormat_ReturnsPayload()
    {
        var payload = await _sut.VerifyIdTokenAsync("mock-google:user@gmail.com:John Doe");

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("user@gmail.com"));
            Assert.That(payload.FullName, Is.EqualTo("John Doe"));
            Assert.That(payload.SubjectId, Is.EqualTo("mock-google-sub-user@gmail.com"));
        });
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithConvenienceStudentToken_ReturnsStudentPayload()
    {
        var payload = await _sut.VerifyIdTokenAsync("mock-google-student-token");

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("student.radar@ictu.edu.vn"));
            Assert.That(payload.FullName, Is.EqualTo("ICTU Demo Student"));
            Assert.That(payload.SubjectId, Is.EqualTo("mock-google-sub-student-01"));
        });
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithConvenienceLandlordToken_ReturnsLandlordPayload()
    {
        var payload = await _sut.VerifyIdTokenAsync("mock-google-landlord-token");

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("landlord.radar@gmail.com"));
            Assert.That(payload.FullName, Is.EqualTo("Demo Landlord"));
            Assert.That(payload.SubjectId, Is.EqualTo("mock-google-sub-landlord-01"));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task VerifyIdTokenAsync_WithEmptyToken_ThrowsInvalidTokenException(string? token)
    {
        var ex = Assert.ThrowsAsync<InvalidTokenException>(async () => await _sut.VerifyIdTokenAsync(token!));
        await Task.CompletedTask;
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithMalformedMockFormat_ThrowsInvalidTokenException()
    {
        var ex = Assert.ThrowsAsync<InvalidTokenException>(async () => await _sut.VerifyIdTokenAsync("mock-google:onlyonepart"));
        await Task.CompletedTask;
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithSignedJwt_ReturnsPayload()
    {
        var testKeyBytes = Encoding.UTF8.GetBytes("a-very-secret-test-key-of-at-least-32-bytes!");
        var securityKey = new SymmetricSecurityKey(testKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = "radar-app",
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "google-12345"),
                new Claim("email", "student@ictu.edu.vn"),
                new Claim("name", "Student Google")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials
        };

        var jwt = tokenHandler.CreateEncodedJwt(descriptor);

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.Zero
        };

        var verifier = new GoogleAuthVerifierAdapter(validationParams);
        var payload = await verifier.VerifyIdTokenAsync(jwt);

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(payload.FullName, Is.EqualTo("Student Google"));
            Assert.That(payload.SubjectId, Is.EqualTo("google-12345"));
        });
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithUnverifiedEmailClaim_ThrowsInvalidTokenException()
    {
        var testKeyBytes = Encoding.UTF8.GetBytes("a-very-secret-test-key-of-at-least-32-bytes!");
        var securityKey = new SymmetricSecurityKey(testKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = "radar-app",
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "google-12345"),
                new Claim("email", "student@ictu.edu.vn"),
                new Claim("email_verified", "false")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials
        };

        var jwt = tokenHandler.CreateEncodedJwt(descriptor);

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.Zero
        };

        var verifier = new GoogleAuthVerifierAdapter(validationParams);
        var ex = Assert.ThrowsAsync<InvalidTokenException>(async () =>
            await verifier.VerifyIdTokenAsync(jwt));
        await Task.CompletedTask;
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithTamperedSignature_ThrowsInvalidTokenException()
    {
        var testKeyBytes = Encoding.UTF8.GetBytes("a-very-secret-test-key-of-at-least-32-bytes!");
        var securityKey = new SymmetricSecurityKey(testKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = "radar-app",
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "google-12345"),
                new Claim("email", "student@ictu.edu.vn")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials
        };

        var jwt = tokenHandler.CreateEncodedJwt(descriptor);
        var tamperedJwt = jwt + "tampered";

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.Zero
        };

        var verifier = new GoogleAuthVerifierAdapter(validationParams);
        var ex = Assert.ThrowsAsync<InvalidTokenException>(async () =>
            await verifier.VerifyIdTokenAsync(tamperedJwt));
        await Task.CompletedTask;
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithMismatchedAudience_ThrowsInvalidTokenException()
    {
        var testKeyBytes = Encoding.UTF8.GetBytes("a-very-secret-test-key-of-at-least-32-bytes!");
        var securityKey = new SymmetricSecurityKey(testKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = "unrelated-foreign-app-id",
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "google-12345"),
                new Claim("email", "student@ictu.edu.vn"),
                new Claim("email_verified", "true")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials
        };

        var jwt = tokenHandler.CreateEncodedJwt(descriptor);

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = true,
            ValidAudience = "roomtorentradar-client-id",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.Zero
        };

        var verifier = new GoogleAuthVerifierAdapter(validationParams);
        var ex = Assert.ThrowsAsync<InvalidTokenException>(async () =>
            await verifier.VerifyIdTokenAsync(jwt));
        await Task.CompletedTask;
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithMatchingAudience_Succeeds()
    {
        var testKeyBytes = Encoding.UTF8.GetBytes("a-very-secret-test-key-of-at-least-32-bytes!");
        var securityKey = new SymmetricSecurityKey(testKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = "roomtorentradar-client-id",
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", "google-12345"),
                new Claim("email", "student@ictu.edu.vn"),
                new Claim("email_verified", "true"),
                new Claim("name", "Student Google")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials
        };

        var jwt = tokenHandler.CreateEncodedJwt(descriptor);

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = true,
            ValidAudience = "roomtorentradar-client-id",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.Zero
        };

        var verifier = new GoogleAuthVerifierAdapter(validationParams);
        var payload = await verifier.VerifyIdTokenAsync(jwt);

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(payload.FullName, Is.EqualTo("Student Google"));
            Assert.That(payload.SubjectId, Is.EqualTo("google-12345"));
        });
    }

    [Test]
    public void VerifyIdTokenAsync_WithoutConfiguredAudienceInProductionMode_ThrowsInvalidOperationException()
    {
        var oldEnv = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", null);
            var verifier = new GoogleAuthVerifierAdapter(customValidationParameters: null, audience: null);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await verifier.VerifyIdTokenAsync("header.payload.signature"));

            Assert.That(ex!.Message, Does.Contain("Google Client ID (Audience) must be configured"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", oldEnv);
        }
    }
}

