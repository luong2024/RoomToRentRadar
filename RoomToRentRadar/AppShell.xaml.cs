using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    private readonly AuthService _authService;

    public AppShell(IServiceProvider services, AuthService authService)
    {
        InitializeComponent();
        _services = services;
        _authService = authService;
        ExploreTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());
        FavouritesTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<FavouritesPage>());
        MapTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MapSearchPage>());
        MyListingsTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MyListingsPage>());
        ModerationTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ModerationQueuePage>());
        ProfileTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ProfilePage>());
        Routing.RegisterRoute(AppRoutes.ListingEditor, typeof(ListingEditorPage));
        Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(RoomDetailPage));
        Routing.RegisterRoute(AppRoutes.Login, typeof(LoginPage));
        Routing.RegisterRoute(AppRoutes.Register, typeof(RegisterPage));
        Routing.RegisterRoute(AppRoutes.OtpVerification, typeof(OtpVerificationPage));
        Routing.RegisterRoute(AppRoutes.Directions, typeof(DirectionsPage));
        Routing.RegisterRoute(AppRoutes.Comparison, typeof(ComparisonPage));
        Routing.RegisterRoute(AppRoutes.EditProfile, typeof(EditProfilePage));
        Routing.RegisterRoute(AppRoutes.AccountSettings, typeof(AccountSettingsPage));

        _authService.SessionChanged += OnSessionChanged;
        _ = RefreshRoleAsync();
    }

    public static string GetRoleRootRoute(AppUserRole? role) => role switch
    {
        AppUserRole.Landlord => $"//{AppRoutes.MyListings}",
        AppUserRole.Moderator or AppUserRole.Admin => $"//{AppRoutes.Moderation}",
        _ => $"//{AppRoutes.Home}"
    };

    public void PrepareRoleTabsForNavigation(AppUserRole? role)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => PrepareRoleTabsForNavigation(role));
            return;
        }

        var isLandlord = role == AppUserRole.Landlord;
        var isModeratorOrAdmin = role is AppUserRole.Moderator or AppUserRole.Admin;

        if (!isModeratorOrAdmin)
        {
            if (!ExploreTab.IsVisible) ExploreTab.IsVisible = true;
            if (!MapTab.IsVisible) MapTab.IsVisible = true;
        }

        if (!isLandlord && !isModeratorOrAdmin)
        {
            if (!FavouritesTab.IsVisible) FavouritesTab.IsVisible = true;
        }

        if (isLandlord && !MyListingsTab.IsVisible)
        {
            MyListingsTab.IsVisible = true;
        }

        if (isModeratorOrAdmin && !ModerationTab.IsVisible)
        {
            ModerationTab.IsVisible = true;
        }
    }

    public void FinalizeRoleTabs(AppUserRole? role)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => FinalizeRoleTabs(role));
            return;
        }

        PrepareRoleTabsForNavigation(role);

        var isLandlord = role == AppUserRole.Landlord;
        var isModeratorOrAdmin = role is AppUserRole.Moderator or AppUserRole.Admin;

        if (isModeratorOrAdmin)
        {
            if (ExploreTab.IsVisible) ExploreTab.IsVisible = false;
            if (MapTab.IsVisible) MapTab.IsVisible = false;
        }

        if (isLandlord || isModeratorOrAdmin)
        {
            if (FavouritesTab.IsVisible) FavouritesTab.IsVisible = false;
        }

        if (!isLandlord && MyListingsTab.IsVisible)
        {
            MyListingsTab.IsVisible = false;
        }

        if (!isModeratorOrAdmin && ModerationTab.IsVisible)
        {
            ModerationTab.IsVisible = false;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => ScheduleRoleSync(_authService.CurrentRole);

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

        ScheduleRoleSync(_authService.CurrentRole);
    }

    private void ScheduleRoleSync(AppUserRole? role)
    {
        PrepareRoleTabsForNavigation(role);
        _ = Dispatcher.DispatchAsync(async () =>
        {
            try
            {
                await Task.Yield();
                var navService = _services.GetService<INavigationService>() as ShellNavigationService;
                if (navService?.IsNavigating == true)
                {
                    return;
                }

                if (CurrentPage is LoginPage or RegisterPage or OtpVerificationPage)
                {
                    return;
                }

                var currentRole = _authService.CurrentRole;
                PrepareRoleTabsForNavigation(currentRole);
                if (!IsCurrentTabAllowed(currentRole))
                {
                    if (navService is not null)
                    {
                        await navService.GoToRoleRootAsync(currentRole);
                        return;
                    }

                    await GoToAsync(GetRoleRootRoute(currentRole), animate: false);
                }

                FinalizeRoleTabs(currentRole);
            }
            catch (Exception)
            {
                // Window or Shell teardown during role sync is non-fatal.
            }
        });
    }

    private bool IsCurrentTabAllowed(AppUserRole? role)
    {
        var activeContent = CurrentItem?.CurrentItem?.CurrentItem;
        if (activeContent is null)
        {
            return false;
        }

        var isLandlord = role == AppUserRole.Landlord;
        var isModeratorOrAdmin = role is AppUserRole.Moderator or AppUserRole.Admin;

        if (ReferenceEquals(activeContent, ExploreTab) || ReferenceEquals(activeContent, MapTab))
        {
            return !isModeratorOrAdmin;
        }

        if (ReferenceEquals(activeContent, FavouritesTab))
        {
            return !isLandlord && !isModeratorOrAdmin;
        }

        if (ReferenceEquals(activeContent, MyListingsTab))
        {
            return isLandlord;
        }

        if (ReferenceEquals(activeContent, ModerationTab))
        {
            return isModeratorOrAdmin;
        }

        return true;
    }
}
