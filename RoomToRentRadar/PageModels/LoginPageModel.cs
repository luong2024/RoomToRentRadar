using System.Net;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.DeviceAdapters;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels;

public partial class LoginPageModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRegex = new(@"^(0|\+84)[0-9]{9}$", RegexOptions.Compiled);

    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;
    private readonly IGoogleAuthClientService _googleAuthService;

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

    public string SignInWithGoogleText => AppResources.SignInWithGoogle;
    public string OrSignInWithEmailText => AppResources.OrSignInWithEmail;

    public LoginPageModel(
        IAuthGateway authGateway,
        ISessionStore sessionStore,
        IGoogleAuthClientService? googleAuthService = null)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _googleAuthService = googleAuthService ?? new GoogleAuthClientService();

        LocalizedStrings.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SignInWithGoogleText));
            OnPropertyChanged(nameof(OrSignInWithEmailText));
        };
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
    private async Task LoginWithGoogleAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            HasGeneralError = false;
            GeneralError = string.Empty;

            var idToken = await _googleAuthService.AuthenticateAndGetIdTokenAsync();
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return;
            }

            var email = JwtPayloadHelper.ExtractEmail(idToken);
            var isIctuStudent = JwtPayloadHelper.IsIctuStudentEmail(email);
            var role = isIctuStudent ? "Student" : "Landlord";

            await ExecuteGoogleLoginWithChallengeHandlingAsync(idToken, role);
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapAuthErrorCodeToMessage(apiEx.ErrorCode, apiEx.StatusCode, apiEx.Message);
            HasGeneralError = true;
        }
        catch (HttpRequestException)
        {
            GeneralError = "Unable to connect to the authentication server. Please check your network.";
            HasGeneralError = true;
        }
        catch (Exception ex)
        {
            GeneralError = !string.IsNullOrWhiteSpace(ex.Message) ? ex.Message : "An unexpected error occurred during Google login.";
            HasGeneralError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteGoogleLoginWithChallengeHandlingAsync(string idToken, string role)
    {
        string? phone = null;
        string? houseName = null;
        string? password = null;
        string? currentPassword = null;

        while (true)
        {
            var request = new GoogleLoginRequest(
                IdToken: idToken,
                Role: role,
                PhoneNumber: phone,
                DeviceInfo: DeviceInfo.Current.Model,
                Password: password,
                CurrentPassword: currentPassword,
                HouseName: houseName);

            try
            {
                var response = await _authGateway.LoginWithGoogleAsync(request);
                await HandleLoginResponseAsync(response);
                return;
            }
            catch (ApiException apiEx) when (apiEx.ErrorCode == "AUTH_PHONE_REQUIRED" && Shell.Current != null)
            {
                var phoneInput = await Shell.Current.DisplayPromptAsync(
                    AppResources.PromptLandlordPhoneTitle,
                    AppResources.PromptLandlordPhoneMessage,
                    accept: AppResources.Confirm,
                    cancel: AppResources.Cancel,
                    placeholder: "0912345678",
                    maxLength: 12,
                    keyboard: Keyboard.Telephone);

                if (string.IsNullOrWhiteSpace(phoneInput))
                {
                    return; // user cancelled prompt
                }

                var trimmedPhone = phoneInput.Trim();
                if (!PhoneRegex.IsMatch(trimmedPhone))
                {
                    await Shell.Current.DisplayAlertAsync(
                        AppResources.PhoneNumberRequiredTitle,
                        AppResources.PhoneNumberRequiredMessage,
                        AppResources.Ok);
                    return;
                }

                phone = trimmedPhone;
            }
            catch (ApiException apiEx) when (apiEx.ErrorCode == "AUTH_HOUSE_NAME_REQUIRED" && Shell.Current != null)
            {
                var houseNameInput = await Shell.Current.DisplayPromptAsync(
                    AppResources.PromptLandlordHouseNameTitle,
                    AppResources.PromptLandlordHouseNameMessage,
                    accept: AppResources.Confirm,
                    cancel: AppResources.Cancel,
                    placeholder: AppResources.HouseNamePlaceholder,
                    maxLength: 100,
                    keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(houseNameInput))
                {
                    return; // user cancelled prompt
                }

                var trimmedHouse = houseNameInput.Trim();
                if (trimmedHouse.Length < 3)
                {
                    await Shell.Current.DisplayAlertAsync(
                        AppResources.HouseNameRequiredTitle,
                        AppResources.HouseNameRequiredMessage,
                        AppResources.Ok);
                    return;
                }

                houseName = trimmedHouse;
            }
            catch (ApiException apiEx) when (apiEx.ErrorCode == "AUTH_PASSWORD_REQUIRED" && Shell.Current != null)
            {
                var passInput = await Shell.Current.DisplayPromptAsync(
                    AppResources.PromptCreatePasswordTitle,
                    AppResources.PromptCreatePasswordMessage,
                    accept: AppResources.Confirm,
                    cancel: AppResources.Cancel,
                    placeholder: "••••••••",
                    maxLength: 64,
                    keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(passInput))
                {
                    return; // user cancelled prompt
                }

                var trimmedPass = passInput.Trim();
                if (trimmedPass.Length < 8)
                {
                    await Shell.Current.DisplayAlertAsync(
                        AppResources.PasswordRequiredTitle,
                        AppResources.PasswordValidationRule,
                        AppResources.Ok);
                    return;
                }

                password = trimmedPass;
            }
            catch (ApiException apiEx) when (apiEx.ErrorCode == "AUTH_CURRENT_PASSWORD_REQUIRED" && Shell.Current != null)
            {
                var currentPassInput = await Shell.Current.DisplayPromptAsync(
                    AppResources.PromptCurrentPasswordTitle,
                    AppResources.PromptCurrentPasswordMessage,
                    accept: AppResources.Confirm,
                    cancel: AppResources.Cancel,
                    placeholder: "••••••••",
                    maxLength: 64,
                    keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(currentPassInput))
                {
                    return; // user cancelled prompt
                }

                currentPassword = currentPassInput.Trim();
            }
        }
    }

    private async Task HandleLoginResponseAsync(AuthTokensDto? response)
    {
        if (response != null && !string.IsNullOrEmpty(response.AccessToken))
        {
            var saved = await _sessionStore.SaveSessionAsync(response);
            if (!saved)
            {
                GeneralError = AppResources.SessionStorageError;
                HasGeneralError = true;
                return;
            }

            try
            {
                await Shell.Current.GoToAsync("//main");
            }
            catch (Exception)
            {
                GeneralError = AppResources.GoogleNavError;
                HasGeneralError = true;
            }
        }
        else
        {
            GeneralError = AppResources.GoogleAuthFailed;
            HasGeneralError = true;
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
                "AUTH_PHONE_REQUIRED" or "PHONE_REQUIRED"
                    => AppResources.PhoneNumberRequiredMessage,
                "AUTH_PASSWORD_REQUIRED" or "PASSWORD_REQUIRED"
                    => AppResources.PasswordValidationRule,
                "AUTH_CURRENT_PASSWORD_REQUIRED"
                    => AppResources.PromptCurrentPasswordMessage,
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
