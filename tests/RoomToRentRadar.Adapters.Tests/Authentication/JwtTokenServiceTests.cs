using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Tests.Authentication;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private JwtOptions _jwtOptions = null!;
    private JwtTokenService _tokenService = null!;

    [SetUp]
    public void SetUp()
    {
        _jwtOptions = new JwtOptions
        {
            Issuer = "RoomToRentRadar.Api",
            Audience = "RoomToRentRadar.Client",
            Secret = "0123456789abcdef0123456789abcdef-strong-key",
            AccessTokenExpirationMinutes = 20,
            RefreshTokenExpirationDays = 14,
            ClockSkewSeconds = 30
        };

        _tokenService = new JwtTokenService(Options.Create(_jwtOptions));
    }

    [Test]
    public async Task GenerateAccessToken_ShouldIncludeRequiredClaimsAndRespectConfiguredLifetime()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("landlord@ictu.edu.vn"),
            "password-hash",
            UserRole.Landlord,
            FixedNow);

        var generated = _tokenService.GenerateAccessToken(user, FixedNow);

        Assert.That(generated.ExpiresAt, Is.EqualTo(FixedNow.AddMinutes(20)));

        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        var validationResult = await handler.ValidateTokenAsync(
            generated.Token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret)),
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtOptions.Audience,
                ValidateLifetime = false
            });

        Assert.Multiple(() =>
        {
            Assert.That(validationResult.IsValid, Is.True);
            var identity = validationResult.ClaimsIdentity!;
            Assert.That(identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, Is.EqualTo(user.Id.ToString()));
            Assert.That(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, Is.EqualTo(user.Id.ToString()));
            Assert.That(identity.FindFirst(JwtRegisteredClaimNames.Email)?.Value, Is.EqualTo("landlord@ictu.edu.vn"));
            Assert.That(identity.FindFirst(ClaimTypes.Role)?.Value, Is.EqualTo("LANDLORD"));
            Assert.That(identity.FindFirst(AuthClaimTypes.Role)?.Value, Is.EqualTo("LANDLORD"));
            Assert.That(identity.FindFirst(AuthClaimTypes.SecurityStamp)?.Value, Is.EqualTo(user.SecurityStamp));
        });
    }

    [Test]
    public void GenerateRefreshToken_ShouldProduceHighEntropyTokenAndSha256HashWithConfiguredExpiry()
    {
        var token1 = _tokenService.GenerateRefreshToken(FixedNow);
        var token2 = _tokenService.GenerateRefreshToken(FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(token1.RawToken, Is.Not.Empty.And.Not.EqualTo(token2.RawToken));
            Assert.That(token1.TokenHash, Is.Not.EqualTo(token1.RawToken));
            Assert.That(token1.TokenHash, Is.EqualTo(_tokenService.HashRefreshToken(token1.RawToken)));
            Assert.That(token1.ExpiresAt, Is.EqualTo(FixedNow.AddDays(14)));
        });
    }
}
