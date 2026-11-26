using CommunityToolkit.Maui.Views;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.ViewModels.RoomDetails;

namespace RoomToRentRadar.Views.RoomDetails;

public partial class ReportListingPopup : Popup
{
    private readonly ReportListingPopupViewModel _viewModel;
    private readonly TaskCompletionSource<ReportListingDialogResult?> _tcs = new();

    public ReportListingPopup(
        string roomTitle,
        double? maxWidth = null,
        double? maxHeight = null,
        int columnCount = 1)
    {
        InitializeComponent();

        if (maxWidth is > 0)
        {
            RootGrid.WidthRequest = maxWidth.Value;
            RootGrid.Padding = maxWidth.Value < 460 ? new Thickness(18) : new Thickness(24);
        }

        if (maxHeight is > 0)
        {
            RootGrid.MaximumHeightRequest = maxHeight.Value;
        }

        ReasonsLayout.Span = Math.Max(1, columnCount);

        _viewModel = new ReportListingPopupViewModel(roomTitle);
        BindingContext = _viewModel;
        Closed += OnPopupClosed;
    }

    public Task<ReportListingDialogResult?> WaitForResultAsync() => _tcs.Task;

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        try
        {
            await CloseAsync();
        }
        catch
        {
            // Suppress exception if popup is already closed or detached
        }
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        var result = _viewModel.TryBuildResult();
        if (result is null)
        {
            return;
        }

        _tcs.TrySetResult(result);
        try
        {
            await CloseAsync();
        }
        catch
        {
            // Suppress exception if popup is already closed or detached
        }
    }
}
