using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Services;

public sealed class SnackbarUndoPrompt : IUndoPrompt
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMilliseconds(250);

    public async Task<bool> ShowAsync(
        string message,
        string actionText,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var actionTapped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var snackbar = Snackbar.Make(
            message,
            action: () => actionTapped.TrySetResult(true),
            actionButtonText: actionText,
            duration: duration);

        // On Android the Snackbar.Show task can complete long before the snackbar is dismissed,
        // so the undo decision waits for the action callback or the visible window instead.
        _ = ShowSnackbarAsync(snackbar, cancellationToken);

        var windowClosed = Task.Delay(duration + ExpiryBuffer, cancellationToken);
        var completed = await Task.WhenAny(actionTapped.Task, windowClosed);
        return completed == actionTapped.Task;
    }

    private static async Task ShowSnackbarAsync(ISnackbar snackbar, CancellationToken cancellationToken)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => snackbar.Show(cancellationToken));
        }
        catch (Exception)
        {
            // The snackbar is best effort; the undo window still expires via its timer.
        }
    }
}
