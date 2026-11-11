using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;

namespace RoomToRentRadar.PageModels;

public partial class LoginPageModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;

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
        IAuthGateway authGateway,
        ISessionStore sessionStore)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
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
            var response = await _authGateway.LoginAsync(request);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                var saved = await _sessionStore.SaveSessionAsync(response);
                if (!saved)
                {
                    GeneralError = "Không thể lưu phiên đăng nhập an toàn vào thiết bị.";
                    HasGeneralError = true;
                    return;
                }

                await Shell.Current.GoToAsync("//uicatalog");
            }
            else
            {
                GeneralError = "Email hoặc mật khẩu không chính xác. Vui lòng kiểm tra lại.";
                HasGeneralError = true;
            }
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapAuthErrorCodeToMessage(apiEx.ErrorCode, apiEx.Message);
            HasGeneralError = true;
        }
        catch (Exception)
        {
            GeneralError = "Không thể kết nối đến máy chủ. Vui lòng kiểm tra mạng hoặc thử lại sau.";
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

#if !DEBUG
        if (Shell.Current != null)
        {
            await Shell.Current.DisplayAlertAsync(
                "ICTU SSO",
                "Tính năng Đăng nhập một chạm ICTU SSO (Google OIDC) đang được cấu hình bảo mật theo Task T13.04. Vui lòng đăng nhập bằng Email trong thời gian này.",
                "Đã hiểu");
        }
        return;
#else
        try
        {
            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;

            // In development simulation:
            var ssoRequest = new IctuSsoLoginRequest("mock-ictu-sso-id-token", DeviceInfo.Current.Model);
            var response = await _authGateway.LoginWithIctuSsoAsync(ssoRequest);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                await _sessionStore.SaveSessionAsync(response);
                await Shell.Current.GoToAsync("//uicatalog");
            }
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapAuthErrorCodeToMessage(apiEx.ErrorCode, "Xác thực qua ICTU SSO không thành công.");
            HasGeneralError = true;
        }
        catch (Exception)
        {
            GeneralError = "Không thể kết nối đến máy chủ xác thực ICTU SSO. Vui lòng thử lại sau.";
            HasGeneralError = true;
        }
        finally
        {
            IsBusy = false;
        }
#endif
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

    private static string MapAuthErrorCodeToMessage(string? errorCode, string defaultMessage)
    {
        return errorCode switch
        {
            "INVALID_CREDENTIALS" or "AUTH_INVALID_CREDENTIALS" => "Email hoặc mật khẩu không chính xác.",
            "ACCOUNT_LOCKED" or "AUTH_ACCOUNT_LOCKED" => "Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.",
            "USER_NOT_FOUND" or "AUTH_USER_NOT_FOUND" => "Tài khoản không tồn tại trên hệ thống.",
            "USER_DISABLED" => "Tài khoản đã bị tạm dừng hoạt động. Vui lòng liên hệ hỗ trợ.",
            _ => !string.IsNullOrWhiteSpace(defaultMessage) ? defaultMessage : "Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin."
        };
    }
}
