using System.Net.Mail;

namespace RoomToRentRadar.Shared.Auth;

/// <summary>Registration limits shared by the API validators and the mobile form.</summary>
public static class AccountRules
{
    public const int EmailMaxLength = 200;
    public const int DisplayNameMaxLength = 120;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public static bool IsEmailValid(string? email)
        => !string.IsNullOrWhiteSpace(email)
            && email.Trim().Length <= EmailMaxLength
            && MailAddress.TryCreate(email.Trim(), out _);

    public static bool IsDisplayNameValid(string? displayName)
        => !string.IsNullOrWhiteSpace(displayName)
            && displayName.Trim().Length <= DisplayNameMaxLength;

    public static bool IsPasswordValid(string? password)
    {
        if (password is null
            || password.Length < PasswordMinLength
            || password.Length > PasswordMaxLength)
        {
            return false;
        }

        var hasLetter = false;
        var hasDigit = false;
        foreach (var character in password)
        {
            hasLetter |= char.IsLetter(character);
            hasDigit |= char.IsDigit(character);
        }

        return hasLetter && hasDigit;
    }
}

/// <summary>Wire values of the roles a user may pick when registering.</summary>
public static class RegistrationRoles
{
    public const string Student = UserRoleWire.Student;
    public const string Landlord = UserRoleWire.Landlord;

    public static readonly IReadOnlyList<string> All = [Student, Landlord];

    public static bool IsValid(string? role)
        => role is Student or Landlord;
}
