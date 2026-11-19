using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Api.Auth
{
    /// <summary>A signed access token and the UTC moment it stops being valid.</summary>
    public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

    /// <summary>
    /// Issues HMAC-SHA256 signed JWT access tokens for US01 / T01.1.
    /// Claim names match the validation settings in JwtAuthExtensions:
    /// "sub" = user id, "role" = Student/Landlord/Admin, plus email and name for display.
    /// </summary>
    public sealed class JwtTokenService
    {
        public const string RoleClaim = "role";

        private readonly JwtOptions _options;
        private readonly SigningCredentials _signingCredentials;
        private readonly TimeProvider _timeProvider;
        private readonly JsonWebTokenHandler _handler = new();

        public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
            _signingCredentials = new SigningCredentials(CreateSigningKey(_options.SigningKey), SecurityAlgorithms.HmacSha256);
        }

        /// <summary>Same key object is used for signing here and for validation in JwtAuthExtensions.</summary>
        public static SymmetricSecurityKey CreateSigningKey(string signingKey) =>
            new(Encoding.UTF8.GetBytes(signingKey));

        /// <summary>Creates a token for a signed-in user. Never put the password hash in a token.</summary>
        public AccessToken CreateToken(UserAccount user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expires = now.AddMinutes(_options.ExpiresMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = expires,
                SigningCredentials = _signingCredentials,
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                    new Claim(RoleClaim, user.Role),
                    // Unique token id: lets a future logout/blacklist feature identify one token.
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                ]),
            };

            return new AccessToken(_handler.CreateToken(descriptor), expires);
        }
    }
}
