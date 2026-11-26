using Microsoft.Maui.ApplicationModel;

namespace RoomToRentRadar.Services.Interactions;

public sealed class ShellAlertPrompt : IAlertPrompt
{
    internal static SemaphoreSlim DialogSemaphore { get; } = new(1, 1);

    public async Task ShowAsync(
        string title,
        string message,
        string dismissText = "OK",
        CancellationToken cancellationToken = default)
    {
        await DialogSemaphore.WaitAsync(cancellationToken);
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is Shell shell)
                {
                    await shell.DisplayAlertAsync(title, message, dismissText);
                }
            });
        }
        finally
        {
            DialogSemaphore.Release();
        }
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string acceptText,
        string cancelText,
        CancellationToken cancellationToken = default)
    {
        await DialogSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is Shell shell)
                {
                    return await shell.DisplayAlertAsync(title, message, acceptText, cancelText);
                }

                return false;
            });
        }
        finally
        {
            DialogSemaphore.Release();
        }
    }
}
