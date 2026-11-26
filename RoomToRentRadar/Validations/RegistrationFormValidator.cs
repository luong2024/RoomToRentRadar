using RoomToRentRadar.Models;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Validations;

public static class RegistrationFormValidator
{
    public const string DisplayNameField = "display_name";
    public const string EmailField = "email";
    public const string PasswordField = "password";
    public const string ConfirmPasswordField = "confirm_password";
    public const string RoleField = "role";

    public static bool TryValidate(
        string displayName,
        string email,
        string password,
        string confirmPassword,
        AppUserRole role,
        out string? error)
    {
        var errors = ValidateFields(displayName, email, password, confirmPassword, role);
        if (errors.Count == 0)
        {
            error = null;
            return true;
        }

        error = errors.Values.First();
        return false;
    }

    public static IReadOnlyDictionary<string, string> ValidateFields(
        string displayName,
        string email,
        string password,
        string confirmPassword,
        AppUserRole role)
    {
        var errors = new Dictionary<string, string>();

        if (!AccountRules.IsDisplayNameValid(displayName))
        {
            errors[DisplayNameField] = $"Nhập họ tên (tối đa {AccountRules.DisplayNameMaxLength} ký tự).";
        }

        if (!AccountRules.IsEmailValid(email))
        {
            errors[EmailField] = "Email không hợp lệ.";
        }

        if (!AccountRules.IsPasswordValid(password))
        {
            errors[PasswordField] = $"Mật khẩu cần {AccountRules.PasswordMinLength}-{AccountRules.PasswordMaxLength} ký tự, gồm cả chữ và số.";
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            errors[ConfirmPasswordField] = "Mật khẩu nhập lại không khớp.";
        }

        if (role is not (AppUserRole.Student or AppUserRole.Landlord))
        {
            errors[RoleField] = "Chọn vai trò Sinh viên hoặc Chủ phòng.";
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string> ValidateLogin(string email, string password)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(email))
        {
            errors[EmailField] = "Nhập email.";
        }
        else if (!AccountRules.IsEmailValid(email))
        {
            errors[EmailField] = "Email không hợp lệ.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors[PasswordField] = "Nhập mật khẩu.";
        }
        else if (password.Length > AccountRules.PasswordMaxLength)
        {
            errors[PasswordField] = $"Mật khẩu tối đa {AccountRules.PasswordMaxLength} ký tự.";
        }

        return errors;
    }

    public static bool HasMinimumPasswordLength(string? password)
        => password is not null
            && password.Length >= AccountRules.PasswordMinLength
            && password.Length <= AccountRules.PasswordMaxLength;

    public static bool HasPasswordLetter(string? password)
        => password is not null && password.Any(char.IsLetter);

    public static bool HasPasswordDigit(string? password)
        => password is not null && password.Any(char.IsDigit);
}
