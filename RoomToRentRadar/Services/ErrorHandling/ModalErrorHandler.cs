using RoomToRentRadar.Extensions;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;

namespace RoomToRentRadar.Services.ErrorHandling;

public sealed class ModalErrorHandler : IErrorHandler
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public void HandleError(Exception ex)
    {
        DisplayAlertAsync(ex).FireAndForgetSafeAsync();
    }

    private async Task DisplayAlertAsync(Exception ex)
    {
        try
        {
            await _semaphore.WaitAsync();
            if (Shell.Current is Shell shell)
                await shell.DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
