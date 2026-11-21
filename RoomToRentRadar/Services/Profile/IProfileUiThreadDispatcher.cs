namespace RoomToRentRadar.Services.Profile;

public interface IProfileUiThreadDispatcher
{
    void Dispatch(Action action);
}
