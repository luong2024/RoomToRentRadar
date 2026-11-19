using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;

namespace RoomToRentRadar.Infrastructure.Photos;

/// <summary>
/// Wraps the .NET MAUI file picker so the listing editor can pick JPEG/PNG
/// photos without depending on MAUI statics.
/// </summary>
public sealed class FilePickerPhotoPicker : IPhotoPicker
{
    private const long LengthProbeLimit = 32L * 1024 * 1024;

    // Every target platform must be listed: MAUI throws when the current
    // platform is missing from the dictionary.
    private static readonly FilePickerFileType PhotoFileTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["image/jpeg", "image/png"],
            [DevicePlatform.iOS] = ["public.jpeg", "public.png"],
            [DevicePlatform.MacCatalyst] = ["public.jpeg", "public.png"],
            [DevicePlatform.WinUI] = [".jpg", ".jpeg", ".png"]
        });

    public async Task<IReadOnlyList<PickedPhoto>> PickPhotosAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Chọn ảnh phòng trọ",
                FileTypes = PhotoFileTypes
            };

            var results = await FilePicker.Default.PickMultipleAsync(options);
            if (results is null)
                return [];

            var photos = new List<PickedPhoto>();

            foreach (var result in results)
            {
                if (result is null || string.IsNullOrWhiteSpace(result.FileName))
                    continue;

                var fileName = result.FileName;
                var contentType = ListingPhotoRules.Normalize(result.ContentType)
                    ?? ListingPhotoRules.FromExtension(fileName);
                var length = await ReadLengthAsync(result);
                var captured = result;

                photos.Add(new PickedPhoto(
                    fileName,
                    contentType ?? ListingPhotoRules.Jpeg,
                    length,
                    () => captured.OpenReadAsync()));
            }

            return photos;
        }
        catch (PermissionException)
        {
            return [];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception)
        {
            // File pickers surface platform-specific failures (missing provider,
            // activity restart, ...). The editor treats them as "nothing picked".
            return [];
        }
    }

    private static async Task<long?> ReadLengthAsync(FileResult result)
    {
        try
        {
            await using var stream = await result.OpenReadAsync();
            if (stream is null)
                return null;

            if (stream.CanSeek)
                return stream.Length;

            // Non-seekable picker streams cannot report a length, so count the
            // bytes up to the probe limit to keep the size pre-check cheap.
            var buffer = new byte[81920];
            long total = 0;
            int read;

            while (total <= LengthProbeLimit
                   && (read = await stream.ReadAsync(buffer)) > 0)
            {
                total += read;
            }

            return total;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
