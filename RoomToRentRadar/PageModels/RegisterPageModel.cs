using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.PageModels;

[QueryProperty(nameof(Role), "role")]
public partial class RegisterPageModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRegex = new(@"^(03|05|07|08|09)\d{8}$", RegexOptions.Compiled);

    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly ISecureTokenStoragePort _tokenStorage;

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
        IRegisterUserUseCase registerUserUseCase,
        ISecureTokenStoragePort tokenStorage)
    {
        _registerUserUseCase = registerUserUseCase;
        _tokenStorage = tokenStorage;
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
            if (string.IsNullOrWhiteSpace(StudentCode))
            {
                StudentCodeError = "Vui lòng nhập mã sinh viên ICTU (ví dụ: DTC19...).";
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
                PhoneNumberError = "Số điện thoại phải gồm 10 chữ số hợp lệ tại Việt Nam.";
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
            var request = new RegisterUserRequest(
                Email: Email.Trim(),
                Password: Password,
                FullName: FullName.Trim(),
                Role: IsStudent ? "Student" : "Landlord",
                PhoneNumber: string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
                StudentCode: IsStudent && !string.IsNullOrWhiteSpace(StudentCode) ? StudentCode.Trim() : null
            );

            var response = await _registerUserUseCase.ExecuteAsync(request);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                await _tokenStorage.SaveSessionAsync(response);
                await Shell.Current.GoToAsync("//uicatalog");
            }
            else
            {
                GeneralError = "Đăng ký thành công! Vui lòng đăng nhập.";
                HasGeneralError = true;
                await Shell.Current.GoToAsync("//login");
            }
        }
        catch (Exception ex)
        {
            GeneralError = ex.Message.Contains("exist") || ex.Message.Contains("Email")
                ? "Email này đã được sử dụng trong hệ thống. Vui lòng đăng nhập."
                : "Không thể hoàn thành đăng ký. Vui lòng thử lại sau.";
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
}
