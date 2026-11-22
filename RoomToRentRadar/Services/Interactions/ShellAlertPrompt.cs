using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;

namespace RoomToRentRadar.Services.Interactions;

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

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string acceptText,
        string cancelText,
        CancellationToken cancellationToken = default)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is Shell shell)
                return await shell.DisplayAlertAsync(title, message, acceptText, cancelText);

            return false;
        });
}
