using RoomToRentRadar.Models;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Validations;

public static class RegistrationFormValidator
{
    public static bool TryValidate(
        string displayName,
        string email,
        string password,
        string confirmPassword,
        AppUserRole role,
        out string? error)
    {
        error = null;

        if (!AccountRules.IsDisplayNameValid(displayName))
        {
            error = $"Nhập họ tên (tối đa {AccountRules.DisplayNameMaxLength} ký tự).";
            return false;
        }

        if (!AccountRules.IsEmailValid(email))
        {
            error = "Email không hợp lệ.";
            return false;
        }

        if (!AccountRules.IsPasswordValid(password))
        {
            error = $"Mật khẩu cần {AccountRules.PasswordMinLength}-{AccountRules.PasswordMaxLength} ký tự, gồm cả chữ và số.";
            return false;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            error = "Mật khẩu nhập lại không khớp.";
            return false;
        }

        if (role is not (AppUserRole.Student or AppUserRole.Landlord))
        {
            error = "Chọn vai trò Sinh viên hoặc Chủ phòng.";
            return false;
        }

        return true;
    }
}
