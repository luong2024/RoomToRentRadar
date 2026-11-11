namespace RoomToRentRadar.Api.Features.Photos;

public static class PhotoUploadLimits
{
    public const int MaxFilesPerRequest = 10;
    public const long MaxFileBytes = 10L * 1024 * 1024;
    public const int MaxPhotosPerListing = 20;
    public const int MaxPixels = 40_000_000;
    public const int MaxThumbnailEdge = 480;
    public const int OriginalJpegQuality = 85;
    public const int ThumbnailJpegQuality = 80;
    public const long MaxRequestBodyBytes = 100L * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };
}
