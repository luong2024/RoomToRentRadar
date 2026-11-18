namespace RoomToRentRadar.Features.Profile.Services;

public interface IProfileUiThreadDispatcher
{
    void Dispatch(Action action);
}
