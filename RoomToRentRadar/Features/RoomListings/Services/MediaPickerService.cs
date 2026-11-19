using RoomToRentRadar.Features.RoomListings.Models;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Default implementation of IMediaPickerService using .NET MAUI FilePicker.
/// </summary>
public class MediaPickerService : IMediaPickerService
{
    public async Task<IReadOnlyList<ListingPhotoItem>> PickPhotosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.jpeg", "public.png" } },
                    { DevicePlatform.Android, new[] { "image/jpeg", "image/png" } },
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.jpeg", "public.png" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Chọn hình ảnh phòng (tối thiểu 3 ảnh)",
                FileTypes = customFileType
            };

            var results = await FilePicker.Default.PickMultipleAsync(options);
            if (results == null)
            {
                return [];
            }

            var photos = new List<ListingPhotoItem>();
            foreach (var file in results)
            {
                long sizeBytes = 0;
                try
                {
                    var fileInfo = new FileInfo(file.FullPath);
                    if (fileInfo.Exists)
                    {
                        sizeBytes = fileInfo.Length;
                    }
                    else
                    {
                        using var stream = await file.OpenReadAsync();
                        sizeBytes = stream.Length;
                    }
                }
                catch
                {
                    // Fallback to estimated size if direct IO is restricted on platform
                    sizeBytes = 1024 * 1024;
                }

                photos.Add(new ListingPhotoItem
                {
                    FilePath = file.FullPath,
                    FileName = file.FileName,
                    FileSizeBytes = sizeBytes,
                    ContentType = file.ContentType ?? "image/jpeg",
                    StreamOpener = () => file.OpenReadAsync()
                });
            }

            return photos;
        }
        catch (Exception)
        {
            return [];
        }
    }
}
