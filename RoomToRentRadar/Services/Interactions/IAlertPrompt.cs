namespace RoomToRentRadar.Services.Interactions;

/// <summary>Shows a modal message such as a success confirmation.</summary>
public interface IAlertPrompt
{
    Task ShowAsync(
        string title,
        string message,
        string dismissText = "OK",
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(
        string title,
        string message,
        string acceptText,
        string cancelText,
        CancellationToken cancellationToken = default);
}
