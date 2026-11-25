using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services.Navigation;

public interface INavigationService
{
    Task GoToRoomDetailAsync(Guid listingId);

    Task GoToLoginAsync();

    Task GoToRegisterAsync();

    /// <summary>Navigates to the home tab for the signed-in role.</summary>
    Task GoToRoleRootAsync(AppUserRole? role);

    Task GoToListingEditorAsync(Guid? listingId = null);

    Task GoToDirectionsAsync(
        double destinationLat,
        double destinationLng,
        double? originLat,
        double? originLng);

    Task GoToComparisonAsync();

    Task GoBackAsync();
}
