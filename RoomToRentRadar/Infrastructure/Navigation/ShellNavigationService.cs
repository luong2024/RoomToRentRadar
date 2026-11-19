using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Infrastructure.Navigation;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToRoomDetailAsync(Guid listingId)
        => GetShell().GoToAsync($"{AppRoutes.RoomDetail}?id={listingId}");

    public Task GoToLoginAsync()
        => GetShell().GoToAsync(AppRoutes.Login);

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

    public Task GoBackAsync()
        => GetShell().GoToAsync("..");

    private static Shell GetShell()
        => Shell.Current ?? throw new InvalidOperationException("Shell navigation requires an active Shell.");
}
