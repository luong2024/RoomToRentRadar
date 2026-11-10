using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar.Core.Extensions;

public static class TaskExtensions
{
    public static async void FireAndForgetSafeAsync(this Task task, IErrorHandler? handler = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            handler?.HandleError(ex);
        }
    }
}
