namespace RoomToRentRadar.Services.ErrorHandling;

public interface IErrorHandler
{
    void HandleError(Exception ex);
}
