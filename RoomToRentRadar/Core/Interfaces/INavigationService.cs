namespace RoomToRentRadar.Core.Interfaces;

public interface INavigationService
{
    Task GoToRoomDetailAsync(Guid listingId);

    Task GoToLoginAsync();

    /// <summary>Opens the listing editor; a null id starts a new listing.</summary>
    Task GoToListingEditorAsync(Guid? listingId = null);

    Task GoToDirectionsAsync(
        double destinationLat,
        double destinationLng,
        double? originLat,
        double? originLng);

    Task GoBackAsync();
}
