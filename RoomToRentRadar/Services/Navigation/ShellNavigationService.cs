using Microsoft.Maui.ApplicationModel;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    private readonly SemaphoreSlim _navigationLock = new(1, 1);

    public bool IsNavigating { get; private set; }

#if WINDOWS
    private const bool DefaultAnimate = false;
#else
    private const bool DefaultAnimate = true;
#endif

    public Task GoToRoomDetailAsync(Guid listingId)
        => NavigateAsync($"{AppRoutes.RoomDetail}?id={listingId}", DefaultAnimate);

    public Task GoToLoginAsync()
        => NavigateAsync(AppRoutes.Login, DefaultAnimate);

    public Task GoToRegisterAsync()
        => NavigateAsync(AppRoutes.Register, DefaultAnimate);

    public Task GoToRegisterAsync(AppUserRole? initialRole)
    {
        if (initialRole is AppUserRole.Student or AppUserRole.Landlord)
        {
            var wireRole = AppUserRoleWire.ToWireValue(initialRole.Value);
            return NavigateAsync($"{AppRoutes.Register}?role={Uri.EscapeDataString(wireRole)}", DefaultAnimate);
        }

        return GoToRegisterAsync();
    }

    public Task GoToRoleRootAsync(AppUserRole? role)
        => RunNavigationAsync(async shell =>
        {
            var route = AppShell.GetRoleRootRoute(role);
            if (shell is AppShell appShell)
            {
                appShell.PrepareRoleTabsForNavigation(role);
                await shell.GoToAsync(route, animate: false);
                appShell.FinalizeRoleTabs(role);
            }
            else
            {
                await shell.GoToAsync(route, animate: false);
            }
        });

    public Task GoToListingEditorAsync(Guid? listingId = null)
        => NavigateAsync(
            listingId is { } id ? $"{AppRoutes.ListingEditor}?id={id}" : AppRoutes.ListingEditor,
            DefaultAnimate);

    public Task GoToDirectionsAsync(
        double destinationLat,
        double destinationLng,
        double? originLat,
        double? originLng)
    {
        var query = FormattableString.Invariant($"to_lat={destinationLat}&to_lng={destinationLng}");

        if (originLat is { } fromLat && originLng is { } fromLng)
        {
            query += FormattableString.Invariant($"&from_lat={fromLat}&from_lng={fromLng}");
        }

        return NavigateAsync($"{AppRoutes.Directions}?{query}", DefaultAnimate);
    }

    public Task GoToComparisonAsync()
        => NavigateAsync(AppRoutes.Comparison, DefaultAnimate);

    public Task GoBackAsync()
        => NavigateAsync("..", DefaultAnimate);

    private Task NavigateAsync(string route, bool animate)
        => RunNavigationAsync(shell => shell.GoToAsync(route, animate));

    private async Task RunNavigationAsync(Func<Shell, Task> navigationAction)
    {
        await _navigationLock.WaitAsync();
        try
        {
            IsNavigating = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var shell = GetShell();
                await navigationAction(shell);
            });
        }
        finally
        {
            IsNavigating = false;
            _navigationLock.Release();
        }
    }

    private static Shell GetShell()
        => Shell.Current ?? throw new InvalidOperationException("Shell navigation requires an active Shell.");
}
