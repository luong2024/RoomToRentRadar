namespace RoomToRentRadar.Services.Profile;

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
