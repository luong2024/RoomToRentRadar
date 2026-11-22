using System.Security.Cryptography;
using System.Text;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class TokenGenerator : ITokenGenerator
{
    private const int TokenBytes = 32;

    public (string Token, string TokenHash) Create()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        return (token, Hash(token));
    }

    public string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
