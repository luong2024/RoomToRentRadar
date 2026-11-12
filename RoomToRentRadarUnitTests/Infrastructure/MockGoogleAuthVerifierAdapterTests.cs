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
    public void VerifyIdTokenAsync_WithEmptyToken_ThrowsInvalidTokenException(string? token)
    {
        Assert.ThrowsAsync<InvalidTokenException>(() => _sut.VerifyIdTokenAsync(token!));
    }

    [Test]
    public void VerifyIdTokenAsync_WithMalformedMockFormat_ThrowsInvalidTokenException()
    {
        Assert.ThrowsAsync<InvalidTokenException>(() => _sut.VerifyIdTokenAsync("mock-google:onlyonepart"));
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
}
