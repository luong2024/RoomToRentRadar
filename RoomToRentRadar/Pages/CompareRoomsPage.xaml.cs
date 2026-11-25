using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class CompareRoomsPage : ContentPage
{
    private readonly CompareRoomsViewModel _viewModel;

    public CompareRoomsPage(CompareRoomsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.ShareComparisonImageRequested += OnShareComparisonImageRequestedAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.AvailableRooms.Count == 0)
        {
            await _viewModel.LoadAvailableRoomsCommand.ExecuteAsync(null);
        }
    }

    private async Task OnShareComparisonImageRequestedAsync()
    {
        try
        {
            var screenshot = await ComparisonGrid.CaptureAsync();
            if (screenshot is null)
            {
                await DisplayAlertAsync("Chia sẻ so sánh", "Không thể chụp bảng so sánh trên thiết bị này.", "Đóng");
                return;
            }

            var filePath = Path.Combine(
                FileSystem.CacheDirectory,
                $"room_comparison_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png");

            await using (var targetStream = File.Create(filePath))
            {
                await screenshot.CopyToAsync(targetStream, ScreenshotFormat.Png);
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Bảng so sánh phòng trọ quanh ICTU",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Chia sẻ so sánh", $"Đã tạo ảnh bảng so sánh (lỗi hộp thoại hệ thống: {ex.Message}).", "Đóng");
        }
    }
}
