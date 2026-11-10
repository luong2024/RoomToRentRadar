using System.Text;
using System.Text.Json;
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
    public async Task VerifyIdTokenAsync_WithValidOidcJwtForIctuDomain_ReturnsStudentPayload()
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

        var payload = await _sut.VerifyIdTokenAsync(fakeJwt);

        Assert.Multiple(() =>
        {
            Assert.That(payload.Email, Is.EqualTo("dtc225180@ictu.edu.vn"));
            Assert.That(payload.FullName, Is.EqualTo("Sinh Vien OIDC"));
            Assert.That(payload.SubjectId, Is.EqualTo("google-oidc-12345"));
            Assert.That(payload.StudentCode, Is.EqualTo("DTC225180"));
        });
    }

    [Test]
    public void VerifyIdTokenAsync_WithNonIctuDomain_ThrowsDomainValidationException()
    {
        Assert.That(
            async () => await _sut.VerifyIdTokenAsync("mock-ictu:outsider@gmail.com:Outsider"),
            Throws.TypeOf<DomainValidationException>());
    }
}

