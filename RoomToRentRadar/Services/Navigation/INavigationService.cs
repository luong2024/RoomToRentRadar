using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services.Navigation;

public interface INavigationService
{
    Task GoToRoomDetailAsync(Guid listingId);

    Task GoToLoginAsync();

    Task GoToRegisterAsync();

    Task GoToRegisterAsync(AppUserRole? initialRole) => GoToRegisterAsync();

    Task GoToOtpVerificationAsync(Guid challengeId, string email) => Task.CompletedTask;

    /// <summary>Navigates to the home tab for the signed-in role.</summary>
    Task GoToRoleRootAsync(AppUserRole? role);

    Task GoToProfileAsync() => Task.CompletedTask;

    Task GoToEditProfileAsync() => Task.CompletedTask;

    Task GoToAccountSettingsAsync() => Task.CompletedTask;

    Task GoToFavouritesAsync() => Task.CompletedTask;

    Task GoToMyListingsAsync() => Task.CompletedTask;

    Task GoToListingEditorAsync(Guid? listingId = null);

    Task GoToDirectionsAsync(
        double destinationLat,
        double destinationLng,
        double? originLat,
        double? originLng);

    Task GoToComparisonAsync();

    Task GoBackAsync();
}
