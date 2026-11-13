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
            string? phoneNumber = null;

            if (SelectedRole == "Landlord")
            {
                if (Shell.Current != null)
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
                        if (phoneInput != null)
                        {
                            await Shell.Current.DisplayAlertAsync(
                                AppResources.PhoneNumberRequiredTitle,
                                AppResources.PhoneNumberRequiredMessage,
                                AppResources.Ok);
                        }
                        return;
                    }

                    phoneNumber = phoneInput.Trim();
                }
            }

            var token = await _googleAuthService.AuthenticateAndGetIdTokenAsync(SelectedRole);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var request = new GoogleLoginRequest(
                IdToken: token,
                Role: SelectedRole,
                PhoneNumber: phoneNumber,
                DeviceInfo: DeviceInfo.Current.Model);

            var response = await _authGateway.LoginWithGoogleAsync(request);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                var saved = await _sessionStore.SaveSessionAsync(response);
                if (saved && Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//main");
                }
            }
        }
        catch (ApiException apiEx)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.GoogleSignInErrorTitle, apiEx.Message, AppResources.Ok);
            }
        }
        catch (Exception ex)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.SignInFailedTitle, ex.Message, AppResources.Ok);
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
