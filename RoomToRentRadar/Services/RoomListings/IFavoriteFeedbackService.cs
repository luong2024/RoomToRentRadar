namespace RoomToRentRadar.Services.RoomListings;

public interface IFavoriteFeedbackService
{
    Task ShowMessageAsync(string message);
    Task ShowUndoAsync(string message, string actionText, Func<Task> action);
}
