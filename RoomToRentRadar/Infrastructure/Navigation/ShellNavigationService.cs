using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Infrastructure.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToRoomDetailAsync(Guid listingId)
        => GetShell().GoToAsync($"{AppRoutes.RoomDetail}?id={listingId}");

    public Task GoToLoginAsync()
        => GetShell().GoToAsync(AppRoutes.Login);

    public Task GoToListingEditorAsync(Guid? listingId = null)
        => listingId is { } id
            ? GetShell().GoToAsync($"{AppRoutes.ListingEditor}?id={id}")
            : GetShell().GoToAsync(AppRoutes.ListingEditor);

    public Task GoBackAsync()
        => GetShell().GoToAsync("..");

    private static Shell GetShell()
        => Shell.Current ?? throw new InvalidOperationException("Shell navigation requires an active Shell.");
}
