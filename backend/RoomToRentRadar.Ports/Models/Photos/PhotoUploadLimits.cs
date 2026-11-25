namespace RoomToRentRadar.Ports.Models.Photos;

public static class PhotoUploadLimits
{
    public const int MinimumPhotosPerListing = 3;
    public const int MaxFilesPerRequest = 10;
    public const long MaxFileBytes = 10L * 1024 * 1024;
    public const int MaxPhotosPerListing = 20;
    public const int MaxPixels = 40_000_000;
    public const int MaxThumbnailEdge = 480;
    public const int OriginalJpegQuality = 85;
    public const int ThumbnailJpegQuality = 80;
    // Reserve space for multipart headers and form fields in addition to ten full-size files.
    public const long MaxRequestBodyBytes = MaxFilesPerRequest * MaxFileBytes + 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };
}

public static class PhotoErrorCodes
{
    public const string ListingNotFound = "listing_not_found";
    public const string ListingUnavailable = "listing_unavailable";
    public const string InvalidPhoto = "invalid_photo";
    public const string PhotoLimitExceeded = "photo_limit_exceeded";
}
