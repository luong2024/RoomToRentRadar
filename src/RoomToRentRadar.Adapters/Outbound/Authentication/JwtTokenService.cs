using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Outbound.Authentication;

/// <summary>
/// Issues HMAC-SHA256 signed JWT access tokens and cryptographically random refresh tokens.
/// Token lifetimes are driven exclusively by <see cref="JwtOptions"/>.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const int RefreshTokenByteLength = 32; // 256 bits of entropy

    private readonly IOptions<JwtOptions> _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options;
    }

    public GeneratedAccessToken GenerateAccessToken(User user, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtOptions = _options.Value;
        var expiresAt = utcNow.AddMinutes(jwtOptions.AccessTokenExpirationMinutes);

        var keyBytes = Encoding.UTF8.GetBytes(jwtOptions.Secret);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var userIdString = user.Id.ToString();
        var roleName = user.Role.ToRoleName();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userIdString),
            new(ClaimTypes.NameIdentifier, userIdString),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(ClaimTypes.Role, roleName),
            new(AuthClaimTypes.Role, roleName),
            new(AuthClaimTypes.SecurityStamp, user.SecurityStamp),
            new("IsVerified", user.IsVerified ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                utcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = utcNow.UtcDateTime,
            IssuedAt = utcNow.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return new GeneratedAccessToken(token, expiresAt);
    }

    public GeneratedRefreshToken GenerateRefreshToken(DateTimeOffset utcNow)
    {
        var jwtOptions = _options.Value;
        var expiresAt = utcNow.AddDays(jwtOptions.RefreshTokenExpirationDays);

        Span<byte> randomBytes = stackalloc byte[RefreshTokenByteLength];
        RandomNumberGenerator.Fill(randomBytes);

        var rawToken = Base64UrlEncoder.Encode(randomBytes.ToArray());
        var tokenHash = HashRefreshToken(rawToken);

        return new GeneratedRefreshToken(rawToken, tokenHash, expiresAt);
    }

    public string HashRefreshToken(string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw new ArgumentException("Raw refresh token cannot be empty.", nameof(rawRefreshToken));
        }

        var tokenBytes = Encoding.UTF8.GetBytes(rawRefreshToken.Trim());
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
