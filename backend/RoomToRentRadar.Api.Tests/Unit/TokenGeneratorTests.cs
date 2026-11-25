using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class TokenGeneratorTests
{
    private static readonly AuthTokenOptions Options = new()
    {
        Issuer = "RoomToRentRadar.Api",
        Audience = "RoomToRentRadar.Tests",
        SigningKey = "unit-test-signing-key-0123456789abcdef"
    };

    private static readonly AuthTokenSubject Subject = new(
        Guid.Parse("cccccccc-0000-0000-0000-000000000003"),
        "jwt.user@example.test",
        "JWT User",
        UserRole.Landlord);

    [Test]
    public void Create_ReturnsSignedJwtCarryingUserClaims()
    {
        var generator = new TokenGenerator(Options);

        var (token, tokenHash) = generator.Create(Subject, DateTime.UtcNow.AddDays(7));

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            token,
            CreateValidationParameters(),
            out var validatedToken);

        Assert.Multiple(() =>
        {
            Assert.That(validatedToken, Is.InstanceOf<JwtSecurityToken>());
            Assert.That(
                principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
                Is.EqualTo(Subject.UserId.ToString()));
            Assert.That(
                principal.FindFirstValue(JwtRegisteredClaimNames.Name),
                Is.EqualTo(Subject.DisplayName));
            Assert.That(
                principal.FindFirstValue(JwtRegisteredClaimNames.Email),
                Is.EqualTo(Subject.Email));
            Assert.That(principal.FindFirstValue("role"), Is.EqualTo(UserRoleWire.Landlord));
            Assert.That(principal.FindFirstValue(JwtRegisteredClaimNames.Jti), Is.Not.Null.And.Not.Empty);
            Assert.That(tokenHash, Is.EqualTo(generator.Hash(token)));
            Assert.That(tokenHash, Is.Not.EqualTo(token));
        });
    }

    [Test]
    public void Create_ForSameSubjectTwice_ReturnsDistinctTokens()
    {
        var generator = new TokenGenerator(Options);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var (first, firstHash) = generator.Create(Subject, expiresAt);
        var (second, secondHash) = generator.Create(Subject, expiresAt);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(secondHash, Is.Not.EqualTo(firstHash));
        });
    }

    [Test]
    public void Create_WithTamperedSignature_FailsValidation()
    {
        var generator = new TokenGenerator(Options);
        var (token, _) = generator.Create(Subject, DateTime.UtcNow.AddDays(7));
        var segments = token.Split('.');
        var signature = segments[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        segments[2] = new string(signature);
        var tampered = string.Join('.', segments);

        Assert.Catch<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                tampered,
                CreateValidationParameters(),
                out _));
    }

    private static TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Options.Issuer,
        ValidateAudience = true,
        ValidAudience = Options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
}
