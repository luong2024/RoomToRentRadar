using Microsoft.Maui.ApplicationModel;
using RoomToRentRadar.Extensions;
using RoomToRentRadar.Services.Interactions;

namespace RoomToRentRadar.Services.ErrorHandling;

public sealed class ModalErrorHandler : IErrorHandler
{
    public void HandleError(Exception ex)
    {
        DisplayAlertAsync(ex).FireAndForgetSafeAsync();
    }

    private static async Task DisplayAlertAsync(Exception ex)
    {
        await ShellAlertPrompt.DialogSemaphore.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    if (Shell.Current is Shell shell)
                    {
                        await shell.DisplayAlertAsync("Error", ex.Message, "OK");
                    }
                }
                catch
                {
                    // Never crash if Shell or WinUI ContentDialog host is closing.
                }
            });
        }
        finally
        {
            ShellAlertPrompt.DialogSemaphore.Release();
        }
    }
}
