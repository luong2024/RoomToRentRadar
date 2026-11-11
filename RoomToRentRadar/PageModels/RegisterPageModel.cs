using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;

namespace RoomToRentRadar.PageModels;

[QueryProperty(nameof(Role), "role")]
public partial class RegisterPageModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // BR-AUTH-03: Vietnamese phone ^(0|\+84)[0-9]{9}$
    private static readonly Regex PhoneRegex = new(@"^(0|\+84)[0-9]{9}$", RegexOptions.Compiled);

    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;

    [ObservableProperty]
    public partial string Role { get; set; } = "Student";

    [ObservableProperty]
    public partial bool IsStudent { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLandlord { get; set; } = false;

    [ObservableProperty]
    public partial string RoleDisplayName { get; set; } = "Sinh viên";

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StudentCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PhoneNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAgreedToTerms { get; set; } = false;

    [ObservableProperty]
    public partial string FullNameError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmailError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StudentCodeError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PhoneNumberError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneralError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGeneralError { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;

    public RegisterPageModel(
        IAuthGateway authGateway,
        ISessionStore sessionStore)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        UpdateRoleState();
    }

    partial void OnRoleChanged(string value)
    {
        UpdateRoleState();
    }

    private void UpdateRoleState()
    {
        IsStudent = !string.Equals(Role, "Landlord", StringComparison.OrdinalIgnoreCase);
        IsLandlord = !IsStudent;
        RoleDisplayName = IsStudent ? "Sinh viên" : "Chủ trọ";
    }

    [RelayCommand]
    private void SwitchRole()
    {
        Role = IsStudent ? "Landlord" : "Student";
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;

        // Reset errors
        FullNameError = string.Empty;
        EmailError = string.Empty;
        StudentCodeError = string.Empty;
        PhoneNumberError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        GeneralError = string.Empty;
        HasGeneralError = false;

        bool isValid = true;

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length < 2)
        {
            FullNameError = "Họ và tên phải có tối thiểu 2 ký tự.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Vui lòng nhập địa chỉ email.";
            isValid = false;
        }
        else if (!EmailRegex.IsMatch(Email.Trim()))
        {
            EmailError = "Email không đúng định dạng.";
            isValid = false;
        }

        if (IsStudent)
        {
            // BR-AUTH-04: Student code is optional, derived from email if omitted
            if (!string.IsNullOrWhiteSpace(StudentCode) && StudentCode.Trim().Length < 5)
            {
                StudentCodeError = "Mã sinh viên không hợp lệ (ví dụ: DTC19...).";
                isValid = false;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                PhoneNumberError = "Chủ trọ bắt buộc nhập số điện thoại để liên hệ.";
                isValid = false;
            }
            else if (!PhoneRegex.IsMatch(PhoneNumber.Trim()))
            {
                PhoneNumberError = "Số điện thoại phải gồm 10 chữ số hợp lệ tại Việt Nam (ví dụ: 0912345678 hoặc +84912345678).";
                isValid = false;
            }
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
        {
            PasswordError = "Mật khẩu phải có tối thiểu 6 ký tự.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ConfirmPasswordError = "Vui lòng xác nhận lại mật khẩu.";
            isValid = false;
        }
        else if (Password != ConfirmPassword)
        {
            ConfirmPasswordError = "Mật khẩu xác nhận không trùng khớp.";
            isValid = false;
        }

        if (!IsAgreedToTerms)
        {
            GeneralError = "Bạn cần đồng ý với Quy chế hoạt động & Điều khoản của RoomToRentRadar.";
            HasGeneralError = true;
            isValid = false;
        }

        if (!isValid) return;

        try
        {
            IsBusy = true;
            var normalizedStudentCode = IsStudent && !string.IsNullOrWhiteSpace(StudentCode) ? StudentCode.Trim().ToUpperInvariant() : null;
            var normalizedPhone = !IsStudent && !string.IsNullOrWhiteSpace(PhoneNumber) ? PhoneNumber.Trim() : null;

            var request = new RegisterUserRequest(
                Email: Email.Trim(),
                Password: Password,
                FullName: FullName.Trim(),
                Role: IsStudent ? "Student" : "Landlord",
                PhoneNumber: normalizedPhone,
                StudentCode: normalizedStudentCode
            );

            var response = await _authGateway.RegisterAsync(request);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                var saved = await _sessionStore.SaveSessionAsync(response);
                if (!saved)
                {
                    GeneralError = "Đăng ký thành công nhưng không thể lưu phiên đăng nhập an toàn.";
                    HasGeneralError = true;
                    return;
                }

                await Shell.Current.GoToAsync("//uicatalog");
            }
            else
            {
                GeneralError = "Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.";
                HasGeneralError = true;
            }
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapRegisterErrorCodeToMessage(apiEx.ErrorCode, apiEx.Message);
            HasGeneralError = true;
        }
        catch (Exception)
        {
            GeneralError = "Không thể hoàn thành đăng ký. Vui lòng thử lại sau.";
            HasGeneralError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await Shell.Current.GoToAsync("//login");
    }

    private static string MapRegisterErrorCodeToMessage(string? errorCode, string defaultMessage)
    {
        return errorCode switch
        {
            "USER_EMAIL_EXISTS" or "AUTH_EMAIL_EXISTS" => "Email này đã được sử dụng trong hệ thống. Vui lòng đăng nhập.",
            "INVALID_STUDENT_CODE" => "Mã sinh viên không hợp lệ.",
            "INVALID_PHONE_NUMBER" => "Số điện thoại không hợp lệ.",
            "INVALID_ROLE" => "Vai trò người dùng không hợp lệ.",
            _ => !string.IsNullOrWhiteSpace(defaultMessage) ? defaultMessage : "Đăng ký thất bại. Vui lòng kiểm tra lại thông tin."
        };
    }
}
