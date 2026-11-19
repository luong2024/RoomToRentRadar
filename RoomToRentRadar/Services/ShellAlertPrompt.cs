using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Services;

public sealed class ShellAlertPrompt : IAlertPrompt
{
    public Task ShowAsync(
        string title,
        string message,
        string dismissText = "OK",
        CancellationToken cancellationToken = default)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is Shell shell)
                await shell.DisplayAlertAsync(title, message, dismissText);
        });
}
