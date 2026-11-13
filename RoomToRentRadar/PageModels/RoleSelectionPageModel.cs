using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.DeviceAdapters;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels;

public partial class RoleSelectionPageModel : ObservableObject
{
    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;
    private readonly IGoogleAuthClientService _googleAuthService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueWithGoogleText))]
    public partial string SelectedRole { get; set; } = "Student";

    [ObservableProperty]
    public partial bool IsStudentSelected { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLandlordSelected { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;

    public string ContinueWithGoogleText => SelectedRole == "Student"
        ? AppResources.ContinueWithGoogleStudent
        : AppResources.ContinueWithGoogleLandlord;

    public string RegisterWithEmailPasswordText => AppResources.RegisterWithEmailPassword;

    public RoleSelectionPageModel(
        IAuthGateway authGateway,
        ISessionStore sessionStore,
        IGoogleAuthClientService? googleAuthService = null)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _googleAuthService = googleAuthService ?? new GoogleAuthClientService();

        LocalizedStrings.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ContinueWithGoogleText));
            OnPropertyChanged(nameof(RegisterWithEmailPasswordText));
        };
    }

    [RelayCommand]
    private void SelectStudent()
    {
        SelectedRole = "Student";
        IsStudentSelected = true;
        IsLandlordSelected = false;
    }

    [RelayCommand]
    private void SelectLandlord()
    {
        SelectedRole = "Landlord";
        IsStudentSelected = false;
        IsLandlordSelected = true;
    }

    [RelayCommand]
    private async Task ContinueWithGoogleAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            var token = await _googleAuthService.AuthenticateAndGetIdTokenAsync(SelectedRole);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            string? currentPassword = null;

            while (true)
            {
                var request = new GoogleLoginRequest(
                    IdToken: token,
                    Role: SelectedRole,
                    DeviceInfo: DeviceInfo.Current.Model,
                    CurrentPassword: currentPassword);

                try
                {
                    var response = await _authGateway.LoginWithGoogleAsync(request);

                    if (response != null && !string.IsNullOrEmpty(response.AccessToken))
                    {
                        var saved = await _sessionStore.SaveSessionAsync(response);
                        if (saved && Shell.Current != null)
                        {
                            await Shell.Current.GoToAsync("//main");
                        }
                    }
                    return;
                }
                catch (ApiException apiEx) when (
                    apiEx.ErrorCode is "AUTH_PASSWORD_REQUIRED" or "AUTH_PHONE_REQUIRED" or "AUTH_HOUSE_NAME_REQUIRED" or "AUTH_HOUSE_ADDRESS_REQUIRED")
                {
                    // First-time Google registration: navigate to RegisterPage for unified onboarding (Phương án B)
                    var email = JwtPayloadHelper.ExtractEmail(token) ?? string.Empty;
                    var fullName = JwtPayloadHelper.ExtractFullName(token) ?? string.Empty;

                    var queryParams = new Dictionary<string, object>
                    {
                        ["role"] = SelectedRole,
                        ["isGoogle"] = "true",
                        ["googleToken"] = token,
                        ["email"] = email,
                        ["fullName"] = fullName
                    };

                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync("register", queryParams);
                    }
                    return;
                }
                catch (ApiException apiEx) when (apiEx.ErrorCode == "AUTH_CURRENT_PASSWORD_REQUIRED" && Shell.Current != null)
                {
                    var curPassInput = await Shell.Current.DisplayPromptAsync(
                        AppResources.PromptCurrentPasswordTitle,
                        AppResources.PromptCurrentPasswordMessage,
                        accept: AppResources.Confirm,
                        cancel: AppResources.Cancel,
                        placeholder: "••••••••",
                        maxLength: 64,
                        keyboard: Keyboard.Text);

                    if (string.IsNullOrWhiteSpace(curPassInput)) return;
                    currentPassword = curPassInput.Trim();
                }
            }
        }
        catch (ApiException apiEx)
        {
            if (Shell.Current != null)
            {
                var message = !string.IsNullOrWhiteSpace(apiEx.Message) ? apiEx.Message : AppResources.GoogleAuthFailed;
                await Shell.Current.DisplayAlertAsync(AppResources.GoogleSignInErrorTitle, message, AppResources.Ok);
            }
        }
        catch (Exception)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.SignInFailedTitle, AppResources.GoogleAuthFailed, AppResources.Ok);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        await Shell.Current.GoToAsync($"register?role={SelectedRole}");
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await Shell.Current.GoToAsync("//login");
    }
}
