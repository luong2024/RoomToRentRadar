using RoomToRentRadar.Models.RoomListings;

namespace RoomToRentRadar.Services.RoomListings;

/// <summary>
/// Default implementation of IMediaPickerService using .NET MAUI FilePicker.
/// </summary>
public class MediaPickerService : IMediaPickerService
{
    public async Task<IReadOnlyList<ListingPhotoItem>> PickPhotosAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            cancellationToken.ThrowIfCancellationRequested();

            if (results == null)
            {
                return [];
            }

            var photos = new List<ListingPhotoItem>();
            foreach (var file in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long sizeBytes = 0;
                try
                {
                    var fileInfo = new FileInfo(file.FullPath);
                    if (fileInfo.Exists && fileInfo.Length > 0)
                    {
                        sizeBytes = fileInfo.Length;
                    }
                    else
                    {
                        using var stream = await file.OpenReadAsync();
                        if (stream != null && stream.Length > 0)
                        {
                            sizeBytes = stream.Length;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // If file size cannot be verified, reject the file
                    sizeBytes = 0;
                }

                if (sizeBytes <= 0)
                {
                    // Reject file whose size or content cannot be determined/read
                    continue;
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }
}
