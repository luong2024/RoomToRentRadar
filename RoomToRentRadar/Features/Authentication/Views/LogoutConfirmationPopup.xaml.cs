using CommunityToolkit.Maui.Views;

namespace RoomToRentRadar.Features.Authentication.Views;

public partial class LogoutConfirmationPopup : Popup
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> WaitForDecisionAsync() => _tcs.Task;

    public LogoutConfirmationPopup()
    {
        InitializeComponent();
        Closed += OnPopupClosed;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(false);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(false);
        try
        {
            await CloseAsync();
        }
        catch
        {
            // Suppress exception if popup is already closed or not attached
        }
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(true);
        try
        {
            await CloseAsync();
        }
        catch
        {
            // Suppress exception if popup is already closed or not attached
        }
    }
}
