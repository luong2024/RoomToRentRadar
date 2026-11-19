namespace RoomToRentRadar.Features.Profile.Services;

public sealed class MauiProfileUiThreadDispatcher : IProfileUiThreadDispatcher
{
    public void Dispatch(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        MainThread.BeginInvokeOnMainThread(action);
    }
}
