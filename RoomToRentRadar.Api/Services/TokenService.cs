using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Entities;
using RoomToRentRadar.Api.Options;

namespace RoomToRentRadar.Api.Services;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs containing User ID and Role (US01 / T01.1, AC2).
/// Claim names are the short JWT names ("sub", "role", "email"). Program.cs must configure
/// JwtBearer with MapInboundClaims = false, NameClaimType = "sub" and RoleClaimType = "role"
/// so that [Authorize(Roles = ...)] and User.FindFirst("sub") read these exact claims.
/// </summary>
public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, string role)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_jwt.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new("role", role),
            // Unique token id: lets us blacklist a single token later if needed.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAtUtc);
    }
}
