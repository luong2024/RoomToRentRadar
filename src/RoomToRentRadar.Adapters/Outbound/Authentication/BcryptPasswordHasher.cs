using System.Text;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.Validators;

namespace RoomToRentRadar.Adapters.Outbound.Authentication;

/// <summary>
/// BCrypt password hasher enforcing the 72-byte UTF-8 ceiling so passwords are never silently truncated.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public const int DefaultWorkFactor = 12;

    private readonly int _workFactor;

    public BcryptPasswordHasher(int workFactor = DefaultWorkFactor)
    {
        if (workFactor is < 10 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(workFactor), "BCrypt work factor must be between 10 and 16.");
        }

        _workFactor = workFactor;
    }

    public string HashPassword(string password)
    {
        EnsurePasswordWithinByteLimit(password);
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: _workFactor);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        if (Encoding.UTF8.GetByteCount(password) > PasswordPolicyValidator.MaxUtf8ByteLength)
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    private static void EnsurePasswordWithinByteLimit(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new RequestValidationException("password", "Password is required.");
        }

        if (Encoding.UTF8.GetByteCount(password) > PasswordPolicyValidator.MaxUtf8ByteLength)
        {
            throw new RequestValidationException(
                "password",
                $"Password must not exceed {PasswordPolicyValidator.MaxUtf8ByteLength} UTF-8 bytes.");
        }
    }
}
