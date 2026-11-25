using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls.Shapes;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.ViewModels.RoomDetails;

namespace RoomToRentRadar.Views.RoomDetails;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.ReportDialogHandler = OpenReportDialogAsync;
    }

    private async Task<ReportListingDialogResult?> OpenReportDialogAsync(string roomId, string roomTitle)
    {
        var availableWidth = Width > 0 ? Width : 640;
        var availableHeight = Height > 0 ? Height : 680;
        var popupWidth = Math.Clamp(availableWidth * 0.90, 280, 580);
        var popupMaxHeight = Math.Clamp(availableHeight * 0.86, 300, 620);
        var columnCount = popupWidth >= 460 ? 2 : 1;

        var popup = new ReportListingPopup(roomTitle, popupWidth, popupMaxHeight, columnCount);
        var options = new PopupOptions
        {
            Shape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            PageOverlayColor = Color.FromArgb("#80000000")
        };
        _ = this.ShowPopupAsync(popup, options);
        return await popup.WaitForResultAsync();
    }

    protected override void OnDisappearing()
    {
        (BindingContext as RoomDetailViewModel)?.PauseTravelInfo();
        base.OnDisappearing();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RoomDetailViewModel viewModel)
        {
            viewModel.ResumeTravelInfo();
            if (viewModel.Room is not null) _ = viewModel.LoadTravelInfoAsync();
        }
    }
}
