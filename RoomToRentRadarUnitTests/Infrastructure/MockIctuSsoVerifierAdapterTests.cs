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
public class MockIctuSsoVerifierAdapterTests
{
    private MockIctuSsoVerifierAdapter _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new MockIctuSsoVerifierAdapter();
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithValidMockIctuToken_ReturnsStudentPayload()
    {
        var payload = await _sut.VerifyIdTokenAsync("mock-ictu:DTC225180@ictu.edu.vn:Nguyen Van A:dtc225180");

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("dtc225180@ictu.edu.vn"));
            Assert.That(payload.FullName, Is.EqualTo("Nguyen Van A"));
            Assert.That(payload.StudentCode, Is.EqualTo("DTC225180"));
            Assert.That(payload.SubjectId, Is.EqualTo("mock-sub-dtc225180@ictu.edu.vn"));
        });
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithFakeSignature_ThrowsInvalidTokenException()
    {
        var header = JwtTokenServiceAdapter.Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var bodyObj = new
        {
            iss = "https://accounts.google.com",
            sub = "google-oidc-12345",
            email = "dtc225180@ictu.edu.vn",
            name = "Sinh Vien OIDC",
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };
        var body = JwtTokenServiceAdapter.Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(bodyObj)));
        var fakeJwt = $"{header}.{body}.signature";

        Assert.ThrowsAsync<InvalidTokenException>(
            async () => await _sut.VerifyIdTokenAsync(fakeJwt));
    }

    [Test]
    public async Task VerifyIdTokenAsync_WithCryptographicallySignedJwt_ReturnsStudentPayload()
    {
        var testKeyBytes = Encoding.UTF8.GetBytes("a-very-secret-test-key-of-at-least-32-bytes!");
        var securityKey = new SymmetricSecurityKey(testKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Expires = DateTime.UtcNow.AddHours(1),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", "google-oidc-signed-999"),
                new Claim("email", "dtc225180@ictu.edu.vn"),
                new Claim("name", "Sinh Vien Chinh Chu"),
                new Claim("student_code", "DTC225180")
            }),
            SigningCredentials = credentials
        };

        var signedToken = tokenHandler.CreateToken(tokenDescriptor);
        var rawSignedJwt = tokenHandler.WriteToken(signedToken);

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = false,
            ValidateLifetime = true
        };

        var verifierWithSigningKey = new IctuSsoVerifierAdapter(validationParams);
        var payload = await verifierWithSigningKey.VerifyIdTokenAsync(rawSignedJwt);

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("dtc225180@ictu.edu.vn"));
            Assert.That(payload.FullName, Is.EqualTo("Sinh Vien Chinh Chu"));
            Assert.That(payload.SubjectId, Is.EqualTo("google-oidc-signed-999"));
            Assert.That(payload.StudentCode, Is.EqualTo("DTC225180"));
        });
    }

    [Test]
    public void VerifyIdTokenAsync_WithNonIctuDomain_ThrowsDomainValidationException()
    {
        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.VerifyIdTokenAsync("mock-ictu:outsider@gmail.com:Outsider"));
    }
}

