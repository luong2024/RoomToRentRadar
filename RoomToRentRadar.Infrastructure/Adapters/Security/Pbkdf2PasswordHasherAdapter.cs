using System.Security.Cryptography;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class Pbkdf2PasswordHasherAdapter : IPasswordHasherPort
{
    private const string FormatVersion = "v1";
    private const int SaltByteSize = 16;
    private const int KeyByteSize = 32;
    private const int DefaultIterations = 100_000;

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or whitespace.", nameof(password));
        }

        Span<byte> salt = stackalloc byte[SaltByteSize];
        RandomNumberGenerator.Fill(salt);

        Span<byte> derivedKey = stackalloc byte[KeyByteSize];
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            derivedKey,
            DefaultIterations,
            HashAlgorithmName.SHA256);

        return string.Create(
            null,
            $"{FormatVersion}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(derivedKey)}");
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], FormatVersion, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedKey = Convert.FromBase64String(parts[3]);

            var actualKey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedKey.Length);

            return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

