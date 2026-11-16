namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>
/// Session lifecycle notifications. Raised from a thread-pool thread after a definitive
/// refresh rejection; UI listeners must marshal to the UI thread themselves (T00.15).
/// </summary>
public interface ISessionEvents
{
    event EventHandler? SessionExpired;

    void RaiseSessionExpired();
}