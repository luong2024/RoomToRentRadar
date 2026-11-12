namespace RoomToRentRadar.Core.Interfaces;

public interface INavigationService
{
    Task GoToRoomDetailAsync(Guid listingId);

    Task GoToLoginAsync();

    Task GoBackAsync();
}
