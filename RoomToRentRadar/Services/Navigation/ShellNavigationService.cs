using RoomToRentRadar.Constants;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;

namespace RoomToRentRadar.Services.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToRoomDetailAsync(Guid listingId)
        => GetShell().GoToAsync($"{AppRoutes.RoomDetail}?id={listingId}");

    public Task GoToLoginAsync()
        => GetShell().GoToAsync(AppRoutes.Login);

    public Task GoToRegisterAsync()
        => GetShell().GoToAsync(AppRoutes.Register);

    public Task GoToRegisterAsync(AppUserRole? initialRole)
    {
        if (initialRole is AppUserRole.Student or AppUserRole.Landlord)
        {
            var wireRole = AppUserRoleWire.ToWireValue(initialRole.Value);
            return GetShell().GoToAsync($"{AppRoutes.Register}?role={Uri.EscapeDataString(wireRole)}");
        }

        return GoToRegisterAsync();
    }

    public Task GoToRoleRootAsync(AppUserRole? role)
        => MainThread.InvokeOnMainThreadAsync(() => GetShell().GoToAsync(role switch
        {
            AppUserRole.Landlord => $"//{AppRoutes.MyListings}",
            AppUserRole.Moderator or AppUserRole.Admin => $"//{AppRoutes.Moderation}",
            _ => $"//{AppRoutes.Home}"
        }));

    public Task GoToListingEditorAsync(Guid? listingId = null)
        => GetShell().GoToAsync(listingId is { } id
            ? $"{AppRoutes.ListingEditor}?id={id}"
            : AppRoutes.ListingEditor);

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

        return GetShell().GoToAsync($"{AppRoutes.Directions}?{query}");
    }

    public Task GoToComparisonAsync()
        => GetShell().GoToAsync(AppRoutes.Comparison);

    public Task GoBackAsync()
        => GetShell().GoToAsync("..");

    private static Shell GetShell()
        => Shell.Current ?? throw new InvalidOperationException("Shell navigation requires an active Shell.");
}
