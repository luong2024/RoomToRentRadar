namespace RoomToRentRadar.Services.Media;

public sealed record PickedListingPhoto(string FileName, string ContentType, string Path);

public interface IListingPhotoPicker
{
    /// <summary>
    /// Opens the device gallery and stages the chosen JPG/PNG files in the cache,
    /// returning their local paths and validated content types.
    /// </summary>
    Task<IReadOnlyList<PickedListingPhoto>?> PickPhotosAsync(
        string title,
        CancellationToken cancellationToken = default);
}
