namespace RoomToRentRadar.Core.Interfaces;

/// <summary>Shows a modal message such as a success confirmation.</summary>
public interface IAlertPrompt
{
    Task ShowAsync(
        string title,
        string message,
        string dismissText = "OK",
        CancellationToken cancellationToken = default);
}
