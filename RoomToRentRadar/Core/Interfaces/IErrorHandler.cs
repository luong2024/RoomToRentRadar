namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// Contract for handling application errors in the presentation layer.
/// </summary>
public interface IErrorHandler
{
    void HandleError(Exception ex);
}
