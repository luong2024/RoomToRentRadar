using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace RoomToRentRadar.Services.Media;

public sealed class MediaPickerAvatarPicker : IAvatarPicker
{
    private static readonly HashSet<string> SupportedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public async Task<string?> PickAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
        {
            Title = "Chọn ảnh đại diện"
        });
        var photo = photos?.FirstOrDefault();

        if (photo is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            extension = ".jpg";
        }

        var directory = Path.Combine(FileSystem.AppDataDirectory, "avatars");
        Directory.CreateDirectory(directory);

        // A timestamped name avoids serving a stale image from the ImageSource cache.
        var destination = Path.Combine(
            directory,
            $"{userId:N}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}");

        await using var source = await photo.OpenReadAsync();
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
        return destination;
    }
}
