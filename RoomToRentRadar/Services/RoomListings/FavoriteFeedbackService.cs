using CommunityToolkit.Maui.Alerts;

namespace RoomToRentRadar.Services.RoomListings;

public sealed class FavoriteFeedbackService : IFavoriteFeedbackService
{
    public async Task ShowMessageAsync(string message)
    {
        try
        {
            await Snackbar.Make(message, duration: TimeSpan.FromSeconds(4)).Show();
        }
        catch (InvalidOperationException)
        {
            // Snackbar presentation is best-effort when there is no active page.
        }
    }

    public async Task ShowUndoAsync(string message, string actionText, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var snackbar = Snackbar.Make(
                message,
                () => MainThread.BeginInvokeOnMainThread(async () => await action()),
                actionText,
                TimeSpan.FromSeconds(5));
            await snackbar.Show();
        }
        catch (InvalidOperationException)
        {
            // The persisted removal remains valid if no page can present the undo action.
        }
    }
}
