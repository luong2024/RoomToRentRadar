using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    private readonly AuthService _authService;

    public AppShell(IServiceProvider services, AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        ExploreTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());
        FavouritesTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<FavouritesPage>());
        MapTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MapSearchPage>());
        MyListingsTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MyListingsPage>());
        ModerationTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ModerationQueuePage>());
        Routing.RegisterRoute(AppRoutes.ListingEditor, typeof(ListingEditorPage));
        Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(RoomDetailPage));
        Routing.RegisterRoute(AppRoutes.Login, typeof(LoginPage));
        Routing.RegisterRoute(AppRoutes.Register, typeof(RegisterPage));
        Routing.RegisterRoute(AppRoutes.Directions, typeof(DirectionsPage));
        Routing.RegisterRoute(AppRoutes.Comparison, typeof(ComparisonPage));

        _authService.SessionChanged += OnSessionChanged;
        _ = RefreshRoleAsync();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => ApplyRole(_authService.CurrentRole);

    private async Task RefreshRoleAsync()
    {
        try
        {
            await _authService.GetCurrentRoleAsync();
        }
        catch (Exception)
        {
            // Role refresh is best effort; the app must still start when secure storage fails.
        }

        ApplyRole(_authService.CurrentRole);
    }

    private void ApplyRole(AppUserRole? role)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => ApplyRole(role));
            return;
        }

        MyListingsTab.IsVisible = role == AppUserRole.Landlord;
        ModerationTab.IsVisible = role is AppUserRole.Moderator or AppUserRole.Admin;
    }
}
