using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.PageModels;

public partial class MainLandingPageModel : ObservableObject
{
    private readonly ISessionStore _sessionStore;

    [ObservableProperty]
    public partial string WelcomeMessage { get; set; } = "Welcome to RoomToRentRadar!";

    [ObservableProperty]
    public partial string UserDisplayName { get; set; } = "Student";

    [ObservableProperty]
    public partial string UserRole { get; set; } = "Guest";

    [ObservableProperty]
    public partial string UserEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUserSignedIn { get; set; } = false;

#if DEBUG
    public bool IsDebugMode => true;
#else
    public bool IsDebugMode => false;
#endif

    public MainLandingPageModel(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    }

    public async Task InitializeAsync()
    {
        var user = await _sessionStore.GetCachedUserProfileAsync();
        if (user != null)
        {
            UserDisplayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email;
            UserRole = user.Role;
            UserEmail = user.Email;
            WelcomeMessage = $"Welcome back, {UserDisplayName}!";
            IsUserSignedIn = true;
        }
        else
        {
            UserDisplayName = "Guest";
            UserRole = "Explorer";
            WelcomeMessage = "Welcome to RoomToRentRadar!";
            IsUserSignedIn = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _sessionStore.ClearSessionAsync();
        await Shell.Current.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task OpenUiCatalogAsync()
    {
        await Shell.Current.GoToAsync("//uicatalog");
    }
}

