namespace RoomToRentRadar.Api.Features.Photos;

public sealed class PhotoStorage(PhotoStorageOptions options, ILogger<PhotoStorage> logger)
{
    public const string RequestPath = "/media";

    public string GetOriginalUrl(Guid listingId, Guid photoId)
        => $"{RequestPath}/listings/{listingId:D}/photo-{photoId:N}.jpg";

    public string GetThumbnailUrl(Guid listingId, Guid photoId)
        => $"{RequestPath}/listings/{listingId:D}/thumbs/photo-{photoId:N}.jpg";

    public async Task SaveAsync(
        Guid listingId,
        Guid photoId,
        byte[] original,
        byte[] thumbnail,
        CancellationToken cancellationToken)
    {
        var originalPath = GetOriginalPath(listingId, photoId);
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
        await File.WriteAllBytesAsync(originalPath, original, cancellationToken);

        var thumbnailPath = GetThumbnailPath(listingId, photoId);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        await File.WriteAllBytesAsync(thumbnailPath, thumbnail, cancellationToken);
    }

    public void Delete(Guid listingId, Guid photoId)
    {
        DeleteFile(GetOriginalPath(listingId, photoId));
        DeleteFile(GetThumbnailPath(listingId, photoId));
    }

    private string GetOriginalPath(Guid listingId, Guid photoId)
        => Path.Combine(options.RootPath, "listings", listingId.ToString("D"), $"photo-{photoId:N}.jpg");

    private string GetThumbnailPath(Guid listingId, Guid photoId)
        => Path.Combine(options.RootPath, "listings", listingId.ToString("D"), "thumbs", $"photo-{photoId:N}.jpg");

    private void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not delete stored photo file {PhotoPath}.", path);
        }
    }
}
