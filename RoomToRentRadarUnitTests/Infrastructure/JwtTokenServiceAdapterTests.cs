using System.Security.Cryptography;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Infrastructure.Adapters.Security;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class JwtTokenServiceAdapterTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
    private JwtTokenServiceAdapter _sut = null!;

    [SetUp]
    public void SetUp()
    {
        var ephemeralKeyBytes = RandomNumberGenerator.GetBytes(32);
        var settings = new JwtSettings
        {
            Issuer = "RoomToRentRadar.Api",
            Audience = "RoomToRentRadar.Client",
            SigningKey = Convert.ToHexString(ephemeralKeyBytes),
            AccessTokenExpirationMinutes = 30,
            RefreshTokenExpirationDays = 7
        };

        _sut = new JwtTokenServiceAdapter(settings);
    }

    [Test]
    public void GenerateAccessToken_EmbedsSubEmailRoleIsVerifiedAndJtiClaims_AndValidatesWithHmacSha256()
    {
        var landlord = User.RegisterLocal(
            email: "landlord@gmail.com",
            passwordHash: "hash",
            fullName: "Chu Tro",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: FixedNow);

        var generated = _sut.GenerateAccessToken(landlord, FixedNow);
        var isValid = _sut.TryValidateAccessToken(generated.Token, FixedNow.AddMinutes(5), out var claims);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(claims, Is.Not.Null);
            Assert.That(claims!["sub"].GetString(), Is.EqualTo(landlord.Id));
            Assert.That(claims["email"].GetString(), Is.EqualTo("landlord@gmail.com"));
            Assert.That(claims["role"].GetString(), Is.EqualTo("Landlord"));
            Assert.That(claims["is_verified"].GetString(), Is.EqualTo("false"));
            Assert.That(claims["jti"].GetString(), Is.EqualTo(generated.JwtId));
        });
    }

    [Test]
    public void TryValidateAccessToken_ExpiredToken_ReturnsFalse()
    {
        var student = User.RegisterLocal(
            email: "student@ictu.edu.vn",
            passwordHash: "hash",
            fullName: "Student",
            role: UserRole.Student,
            phoneNumber: null,
            studentCode: "DTC01",
            utcNow: FixedNow);

        var generated = _sut.GenerateAccessToken(student, FixedNow);
        var isValidAfterExpiry = _sut.TryValidateAccessToken(generated.Token, FixedNow.AddMinutes(31), out _);

        Assert.That(isValidAfterExpiry, Is.False);
    }

    [Test]
    public void Constructor_WithShortOrEmptySigningKey_ThrowsInvalidOperationException()
    {
        Assert.That(
            () => new JwtTokenServiceAdapter(new JwtSettings { SigningKey = "too-short" }),
            Throws.TypeOf<InvalidOperationException>());
    }
}

