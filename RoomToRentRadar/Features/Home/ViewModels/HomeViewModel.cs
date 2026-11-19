using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly Func<string, Task>? _navigateAsync;
    private readonly IAuthenticationSessionService? _sessionService;

    public HomeViewModel() : this(null, null)
    {
    }

    public HomeViewModel(IAuthenticationSessionService sessionService) : this(null, sessionService)
    {
    }

    internal HomeViewModel(Func<string, Task>? navigateAsync, IAuthenticationSessionService? sessionService = null)
    {
        _navigateAsync = navigateAsync;
        _sessionService = sessionService;
        UpdateLocalizedStrings();
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLocalizedStrings();

        if (_sessionService != null)
        {
            _sessionService.CurrentUserChanged += (_, _) => UpdateAuthState();
            UpdateAuthState();
        }
    }

    private void UpdateLocalizedStrings()
    {
        Title = LocalizationResourceManager.Instance["App_Name"];
        Subtitle = LocalizationResourceManager.Instance["Home_Subtitle"];
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "RoomToRentRadar";

    [ObservableProperty]
    public partial string Subtitle { get; set; } = "Find your ideal room around ICTU campus";

    [ObservableProperty]
    public partial string QuickSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAuthenticated { get; set; }

    [ObservableProperty]
    public partial bool IsNotAuthenticated { get; set; } = true;

    private void UpdateAuthState()
    {
        void Apply()
        {
            IsAuthenticated = _sessionService?.IsAuthenticated == true;
            IsNotAuthenticated = !IsAuthenticated;
        }

        try
        {
            if (MainThread.IsMainThread)
            {
                Apply();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(Apply);
            }
        }
        catch
        {
            Apply();
        }
    }

    [RelayCommand]
    public void OpenMenu()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Microsoft.Maui.Controls.Shell.Current is not null)
        {
            Microsoft.Maui.Controls.Shell.Current.FlyoutIsPresented = true;
        }
#endif
    }

    [RelayCommand]
    public void OpenNotifications()
    {
    }

    [RelayCommand]
    public void OpenProfile()
    {
    }

    [RelayCommand]
    public async Task NavigateToAuthAsync()
    {
        await NavigateToRouteAsync("//auth/login");
    }

    [RelayCommand]
    public async Task ExploreRoomsAsync()
    {
        await QuickFilterAsync(QuickSearchText);
    }

    [RelayCommand]
    public async Task SearchFromHomeAsync()
    {
        await QuickFilterAsync(QuickSearchText);
    }

    [RelayCommand]
    public async Task QuickFilterAsync(string? keyword)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;
        var route = string.IsNullOrEmpty(trimmed)
            ? $"//{AppRoutes.RoomListings}"
            : $"//{AppRoutes.RoomListings}?search={Uri.EscapeDataString(trimmed)}";

        await NavigateToRouteAsync(route);
    }

    private async Task NavigateToRouteAsync(string route)
    {
        if (_navigateAsync is not null)
        {
            await _navigateAsync(route);
            return;
        }

#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(route);
        }
#endif
    }
}
