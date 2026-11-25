using System.Security.Cryptography;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    // Fixed PBKDF2 output of a throwaway password; regenerated only if Iterations changes.
    private const string DummySalt = "kQFs1gIB46YCg/UjLjxf4g==";
    private const string DummyHash = "2vZL/kGvvk+UFE/8EM5c+neGIFbXMIl/jbqG3z19FgY=";

    public (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expected = Convert.FromBase64String(hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool VerifyDummy(string password)
        => Verify(password, DummyHash, DummySalt);
}
