using System.Text;
using RoomToRentRadar.Application.Exceptions;

namespace RoomToRentRadar.Application.Validators;

/// <summary>
/// Enforces password complexity and BCrypt 72-byte UTF-8 ceiling to prevent silent truncation.
/// </summary>
public static class PasswordPolicyValidator
{
    public const int MinCharacterLength = 8;
    public const int MaxUtf8ByteLength = 72;

    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new RequestValidationException("password", "Password is required.");
        }

        if (password.Length < MinCharacterLength)
        {
            throw new RequestValidationException(
                "password",
                $"Password must be at least {MinCharacterLength} characters long.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(password);
        if (byteCount > MaxUtf8ByteLength)
        {
            throw new RequestValidationException(
                "password",
                $"Password must not exceed {MaxUtf8ByteLength} UTF-8 bytes.");
        }

        var hasLetter = false;
        var hasDigit = false;

        foreach (var ch in password)
        {
            if (char.IsLetter(ch))
            {
                hasLetter = true;
            }
            else if (char.IsDigit(ch))
            {
                hasDigit = true;
            }
        }

        if (!hasLetter || !hasDigit)
        {
            throw new RequestValidationException(
                "password",
                "Password must contain at least one letter and one digit.");
        }
    }
}
