namespace RoomToRentRadar.Services.Interactions;

/// <summary>
/// Thin platform seam over the snackbar so the undo timing logic stays
/// testable without a UI handler.
/// </summary>
public interface IUndoPrompt
{
    /// <summary>
    /// Shows the message with an action button and completes with <c>true</c>
    /// when the action was tapped before the duration elapsed.
    /// </summary>
    Task<bool> ShowAsync(
        string message,
        string actionText,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
