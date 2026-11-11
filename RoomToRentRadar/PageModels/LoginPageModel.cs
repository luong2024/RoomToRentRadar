using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.PageModels;

public partial class LoginPageModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILoginUserUseCase _loginUserUseCase;
    private readonly ILoginWithIctuSsoUseCase _loginWithIctuSsoUseCase;
    private readonly ISecureTokenStoragePort _tokenStorage;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmailError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneralError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGeneralError { get; set; } = false;

    [ObservableProperty]
    public partial bool IsRememberMe { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;

    public LoginPageModel(
        ILoginUserUseCase loginUserUseCase,
        ILoginWithIctuSsoUseCase loginWithIctuSsoUseCase,
        ISecureTokenStoragePort tokenStorage)
    {
        _loginUserUseCase = loginUserUseCase;
        _loginWithIctuSsoUseCase = loginWithIctuSsoUseCase;
        _tokenStorage = tokenStorage;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        // Reset errors
        EmailError = string.Empty;
        PasswordError = string.Empty;
        GeneralError = string.Empty;
        HasGeneralError = false;

        bool isValid = true;

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Vui lòng nhập địa chỉ email.";
            isValid = false;
        }
        else if (!EmailRegex.IsMatch(Email.Trim()))
        {
            EmailError = "Định dạng email không hợp lệ (ví dụ: ten@ictu.edu.vn).";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Vui lòng nhập mật khẩu.";
            isValid = false;
        }

        if (!isValid) return;

        try
        {
            IsBusy = true;
            var request = new LoginRequest(Email.Trim(), Password);
            var response = await _loginUserUseCase.ExecuteAsync(request);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                await _tokenStorage.SaveSessionAsync(response);
                await Shell.Current.GoToAsync("//uicatalog");
            }
            else
            {
                GeneralError = "Email hoặc mật khẩu không chính xác. Vui lòng kiểm tra lại.";
                HasGeneralError = true;
            }
        }
        catch (Exception ex)
        {
            GeneralError = ex.Message.Contains("credential") || ex.Message.Contains("password")
                ? "Email hoặc mật khẩu không chính xác."
                : "Không thể kết nối đến máy chủ. Vui lòng thử lại sau.";
            HasGeneralError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithIctuSsoAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;

            // In production this integrates with ICTU CAS/OAuth2 web flow.
            // Using student test SSO token for development simulation:
            var ssoRequest = new IctuSsoLoginRequest("mock-ictu-sso-id-token", DeviceInfo.Current.Model);
            var response = await _loginWithIctuSsoUseCase.ExecuteAsync(ssoRequest);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                await _tokenStorage.SaveSessionAsync(response);
                await Shell.Current.GoToAsync("//uicatalog");
            }
        }
        catch (Exception)
        {
            GeneralError = "Xác thực qua ICTU SSO tạm thời gián đoạn. Vui lòng thử lại hoặc dùng Email.";
            HasGeneralError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToRegisterAsync()
    {
        await Shell.Current.GoToAsync("roleselection");
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Quên mật khẩu",
                "Tính năng khôi phục mật khẩu qua Email đang được bảo trì. Vui lòng liên hệ ban quản trị ICTU Radar.",
                "Đã hiểu");
        }
    }
}
