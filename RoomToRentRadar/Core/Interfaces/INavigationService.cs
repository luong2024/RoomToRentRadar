namespace RoomToRentRadar.Core.Interfaces;

public interface INavigationService
{
    Task GoToRoomDetailAsync(Guid listingId);

    Task GoToLoginAsync();

    Task GoToListingEditorAsync(Guid? listingId = null);

    Task GoToDirectionsAsync(
        double destinationLat,
        double destinationLng,
        double? originLat,
        double? originLng);

    Task GoToComparisonAsync();

    Task GoBackAsync();
}
