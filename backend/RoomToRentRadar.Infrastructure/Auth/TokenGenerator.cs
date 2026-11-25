using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class TokenGenerator : ITokenGenerator
{
    private const string RoleClaim = "role";

    private readonly AuthTokenOptions options;
    private readonly JwtSecurityTokenHandler handler = new();
    private readonly SigningCredentials credentials;

    public TokenGenerator(AuthTokenOptions options)
    {
        this.options = options;
        credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public (string Token, string TokenHash) Create(AuthTokenSubject subject, DateTime expiresAt)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, subject.DisplayName),
                new Claim(JwtRegisteredClaimNames.Email, subject.Email),
                new Claim(RoleClaim, UserRoleWire.ToWireValue(subject.Role)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = credentials
        };

        var token = handler.CreateEncodedJwt(descriptor);
        return (token, Hash(token));
    }

    public string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
