namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// A photo the user selected from the device. <see cref="OpenReadAsync"/> is
/// repeatable so the upload can be retried.
/// </summary>
public sealed record PickedPhoto(
    string FileName,
    string? ContentType,
    long? Length,
    Func<Task<Stream>> OpenReadAsync);

public interface IPhotoPicker
{
    /// <summary>
    /// Opens the platform photo/file picker for JPEG and PNG images. Returns an
    /// empty list when the user cancels or the permission is denied.
    /// </summary>
    Task<IReadOnlyList<PickedPhoto>> PickPhotosAsync();
}
