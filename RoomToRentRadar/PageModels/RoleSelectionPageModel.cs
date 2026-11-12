using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;

namespace RoomToRentRadar.PageModels;

public partial class RoleSelectionPageModel : ObservableObject
{
    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;

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
        ? "Continue with Google (Student)"
        : "Continue with Google (Landlord)";

    public RoleSelectionPageModel(IAuthGateway authGateway, ISessionStore sessionStore)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
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
                        "Landlord Phone Number",
                        "Please enter your Vietnamese contact phone number (required for landlord verification):",
                        accept: "Confirm",
                        cancel: "Cancel",
                        placeholder: "0912345678",
                        maxLength: 12,
                        keyboard: Keyboard.Telephone);

                    if (string.IsNullOrWhiteSpace(phoneInput))
                    {
                        if (phoneInput != null)
                        {
                            await Shell.Current.DisplayAlertAsync(
                                "Phone Number Required",
                                "A valid phone number is required to register as a Landlord.",
                                "OK");
                        }
                        return;
                    }

                    phoneNumber = phoneInput.Trim();
                }
            }

            var token = SelectedRole == "Student"
                ? "mock-google-student-token"
                : "mock-google-landlord-token";

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
                await Shell.Current.DisplayAlertAsync("Google Sign-In Error", apiEx.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync("Sign In Failed", ex.Message, "OK");
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
