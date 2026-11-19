using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Features.Comparison.ViewModels;

namespace RoomToRentRadar.Features.Comparison.Views;

public partial class ComparisonPage : ContentPage
{
    private readonly ComparisonViewModel _viewModel;
    private bool _isSharing;

    public ComparisonPage(ComparisonViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        _viewModel.Cancel();
        base.OnDisappearing();
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (_isSharing)
        {
            return;
        }

        _isSharing = true;
        _viewModel.IsCapturing = true;

        try
        {
            // Give the layout one frame to reflow without the navigation chrome.
            await Task.Delay(120);

            var screenshot = await Screenshot.Default.CaptureAsync();
            await using var source = await screenshot.OpenReadAsync();

            var path = Path.Combine(
                FileSystem.CacheDirectory,
                $"so-sanh-phong-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            await using (var file = File.Create(path))
            {
                await source.CopyToAsync(file);
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "So sánh phòng trọ",
                File = new ShareFile(path, "image/png")
            });
        }
        catch (Exception exception)
        {
            _viewModel.ReportShareFailure(exception);
        }
        finally
        {
            _viewModel.IsCapturing = false;
            _isSharing = false;
        }
    }
}
