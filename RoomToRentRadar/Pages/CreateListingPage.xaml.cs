using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class CreateListingPage : ContentPage
{
    private readonly CreateListingViewModel _viewModel;

    public CreateListingPage(CreateListingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.PickPhotosRequested += OnPickPhotosRequestedAsync;
    }

    private async Task OnPickPhotosRequestedAsync()
    {
        try
        {
            var imageFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "image/jpeg", "image/png" } },
                { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png" } },
                { DevicePlatform.iOS, new[] { "public.jpeg", "public.png" } },
                { DevicePlatform.MacCatalyst, new[] { "public.jpeg", "public.png" } }
            });

            var pickedFiles = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Chọn tối thiểu 3 ảnh phòng trọ (JPG/PNG ≤ 10MB)",
                FileTypes = imageFileType
            });

            if (pickedFiles is null)
                return;

            foreach (var file in pickedFiles)
            {
                long sizeBytes = 0;
                if (!string.IsNullOrWhiteSpace(file.FullPath) && File.Exists(file.FullPath))
                {
                    sizeBytes = new FileInfo(file.FullPath).Length;
                }
                else
                {
                    await using var stream = await file.OpenReadAsync();
                    sizeBytes = stream.CanSeek ? stream.Length : 1_024_000;
                }

                var (success, error) = _viewModel.TryAddPhoto(
                    file.FileName,
                    file.ContentType ?? string.Empty,
                    sizeBytes,
                    file.FullPath);

                if (!success && !string.IsNullOrWhiteSpace(error))
                {
                    await DisplayAlertAsync("Ảnh không hợp lệ", error, "Đã hiểu");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Không thể chọn ảnh", ex.Message, "Đóng");
        }
    }
}
