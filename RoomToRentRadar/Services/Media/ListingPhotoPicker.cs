using RoomToRentRadar.Validations;

namespace RoomToRentRadar.Services.Media;

public sealed class ListingPhotoPicker : IListingPhotoPicker
{
    public async Task<IReadOnlyList<PickedListingPhoto>?> PickPhotosAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["image/jpeg", "image/png"],
                [DevicePlatform.iOS] = ["public.jpeg", "public.png"],
                [DevicePlatform.MacCatalyst] = ["public.jpeg", "public.png"],
                [DevicePlatform.WinUI] = [".jpg", ".jpeg", ".png"]
            })
        });

        if (files is null)
        {
            return null;
        }

        var staged = new List<PickedListingPhoto>();
        foreach (var file in files)
        {
            if (file is null)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var fileName = file.FileName ?? "photo.jpg";
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var path = Path.Combine(
                FileSystem.CacheDirectory,
                $"listing-photo-{Guid.NewGuid():N}{extension}");

            try
            {
                string contentType;
                await using (var input = await file.OpenReadAsync())
                await using (var output = File.Create(path))
                {
                    contentType = await ListingPhotoSelectionValidator.CopyValidatedAsync(
                        fileName,
                        input,
                        output,
                        cancellationToken);
                }

                staged.Add(new PickedListingPhoto(fileName, contentType, path));
            }
            catch
            {
                TryDelete(path);
                throw;
            }
        }

        return staged;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
