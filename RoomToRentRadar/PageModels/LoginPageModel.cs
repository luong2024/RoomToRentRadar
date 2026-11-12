using System.Net;
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

#if DEBUG
    public bool IsSsoVisible => true;
#else
    public bool IsSsoVisible => false;
#endif

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
            EmailError = "Please enter your email address.";
            isValid = false;
        }
        else if (!EmailRegex.IsMatch(Email.Trim()))
        {
            EmailError = "Invalid email format (e.g., student@ictu.edu.vn).";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Please enter your password.";
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
                    GeneralError = "Unable to securely store your session on this device.";
                    HasGeneralError = true;
                    return;
                }

                try
                {
                    await Shell.Current.GoToAsync("//main");
                }
                catch (Exception)
                {
                    GeneralError = "Sign in succeeded, but navigation to home screen failed.";
                    HasGeneralError = true;
                }
            }
            else
            {
                GeneralError = "Invalid email or password. Please try again.";
                HasGeneralError = true;
            }
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapAuthErrorCodeToMessage(apiEx.ErrorCode, apiEx.StatusCode, apiEx.Message);
            HasGeneralError = true;
        }
        catch (HttpRequestException)
        {
            GeneralError = "Unable to connect to the server. Please check your internet connection.";
            HasGeneralError = true;
        }
        catch (Exception)
        {
            GeneralError = "An unexpected error occurred. Please try again.";
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
        return;
#else
        try
        {
            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;

            var ssoRequest = new IctuSsoLoginRequest("mock-ictu-sso-id-token", DeviceInfo.Current.Model);
            var response = await _authGateway.LoginWithIctuSsoAsync(ssoRequest);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                var saved = await _sessionStore.SaveSessionAsync(response);
                if (!saved)
                {
                    GeneralError = "Unable to securely store your session on this device.";
                    HasGeneralError = true;
                    return;
                }

                try
                {
                    await Shell.Current.GoToAsync("//main");
                }
                catch (Exception)
                {
                    GeneralError = "SSO sign in succeeded, but navigation to home screen failed.";
                    HasGeneralError = true;
                }
            }
            else
            {
                GeneralError = "Authentication with ICTU SSO failed. Please try again.";
                HasGeneralError = true;
            }
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapAuthErrorCodeToMessage(apiEx.ErrorCode, apiEx.StatusCode, "ICTU SSO authentication failed.");
            HasGeneralError = true;
        }
        catch (HttpRequestException)
        {
            GeneralError = "Unable to connect to the authentication server. Please check your network.";
            HasGeneralError = true;
        }
        catch (Exception)
        {
            GeneralError = "An unexpected error occurred during ICTU SSO login.";
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
                "Forgot Password",
                "Password recovery via email is currently under maintenance. Please contact the ICTU Radar administrator.",
                "OK");
        }
    }

    private static string MapAuthErrorCodeToMessage(string? errorCode, HttpStatusCode statusCode, string defaultMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            return errorCode switch
            {
                "INVALID_CREDENTIALS" or "AUTH_INVALID_CREDENTIALS" or "USER_NOT_FOUND" or "AUTH_USER_NOT_FOUND"
                    => "Invalid email or password.",
                "ACCOUNT_LOCKED" or "AUTH_ACCOUNT_LOCKED"
                    => "Your account is temporarily locked due to multiple failed login attempts. Please try again after 15 minutes.",
                "USER_DISABLED"
                    => "This account has been deactivated. Please contact support.",
                "LANDLORD_NOT_APPROVED" or "AUTH_LANDLORD_NOT_APPROVED"
                    => "Your landlord account is pending administrative verification.",
                _ => !string.IsNullOrWhiteSpace(defaultMessage) ? defaultMessage : "Sign in failed. Please check your credentials."
            };
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid email or password.",
            HttpStatusCode.Forbidden => "Your account is locked or suspended.",
            HttpStatusCode.NotFound => "Invalid email or password.",
            _ => !string.IsNullOrWhiteSpace(defaultMessage) ? defaultMessage : "Sign in failed. Please check your credentials."
        };
    }
}
