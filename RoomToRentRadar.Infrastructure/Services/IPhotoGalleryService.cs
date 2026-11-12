namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Photo upload pipeline and gallery for US04 / T04.1 (consumed by T04.2 PhotoGallery360Page).
    /// Storage follows the shared schema: 2D photos live in RoomListing.PhotosJson (JSON array of strings)
    /// and the single 360° photo lives in RoomListing.Panorama360Url. No new tables are created.
    /// </summary>
    public interface IPhotoGalleryService
    {
        /// <summary>
        /// Returns the gallery of a room (anyone may view). Returns null if the room does not exist.
        /// </summary>
        Task<RoomGallery?> GetGalleryAsync(int roomId);

        /// <summary>
        /// Validates, re-encodes (strips EXIF/GPS) and stores a photo plus its thumbnail.
        /// Only the room's landlord or an Admin may upload.
        /// When <paramref name="isPanorama"/> is true the image must be 2:1 and replaces the existing 360° photo.
        /// </summary>
        /// <param name="fileName">Original name, used only for logging/extension hints; never used as a path.</param>
        Task<PhotoOperationResult> AddPhotoAsync(
            int roomId, Stream content, string fileName, bool isPanorama, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a 2D photo or the 360° photo identified by <see cref="RoomPhoto.Key"/>.
        /// Only the room's landlord or an Admin may remove.
        /// </summary>
        Task<PhotoOperationResult> RemovePhotoAsync(int roomId, string photoKey);

        /// <summary>
        /// Reorders 2D photos. <paramref name="orderedKeys"/> must contain exactly the current photo keys.
        /// Only the room's landlord or an Admin may reorder.
        /// </summary>
        Task<PhotoOperationResult> ReorderPhotosAsync(int roomId, IReadOnlyList<string> orderedKeys);
    }

    /// <summary>
    /// Platform image codec used by PhotoGalleryService. Infrastructure is pure .NET (no MAUI), so decoding,
    /// JPEG re-encoding and thumbnail resizing are implemented in the MAUI app (MauiPhotoImageProcessor)
    /// and injected here. Unit tests can supply a fake.
    /// </summary>
    public interface IPhotoImageProcessor
    {
        /// <summary>
        /// Decodes <paramref name="imageBytes"/> (already checked as JPEG/PNG/WebP by signature),
        /// re-encodes it as JPEG (which drops EXIF/GPS metadata) and builds a thumbnail no wider than
        /// <paramref name="thumbnailMaxWidth"/>. Returns null if the bytes cannot be decoded as an image.
        /// </summary>
        Task<ProcessedPhoto?> ProcessAsync(
            byte[] imageBytes, int thumbnailMaxWidth, float jpegQuality, CancellationToken cancellationToken = default);
    }

    /// <summary>Output of <see cref="IPhotoImageProcessor"/>: pixel size plus the two JPEG files to store.</summary>
    public sealed record ProcessedPhoto(int Width, int Height, byte[] OriginalJpeg, byte[] ThumbnailJpeg);

    /// <summary>Limits agreed for US04 (AC1 requires at least 8 photos per listing).</summary>
    public static class PhotoGalleryLimits
    {
        public const int MinRecommendedPhotos = 8;
        public const int MaxPhotosPerRoom = 20;
        public const long MaxFileSizeBytes = 15L * 1024 * 1024;
        public const int ThumbnailMaxWidth = 400;

        /// <summary>Re-encode quality for originals (0-1). Re-encoding removes EXIF/GPS metadata.</summary>
        public const float OriginalJpegQuality = 0.9f;

        /// <summary>Equirectangular 360° images are 2:1; small tolerance for rounding by cameras.</summary>
        public const double PanoramaAspectRatio = 2.0;
        public const double PanoramaAspectTolerance = 0.02;
    }

    /// <summary>All photos of one room, ready for the gallery UI.</summary>
    /// <param name="Photos">2D photos in display order (for CarouselView).</param>
    /// <param name="Panorama">The 360° photo for the Pannellum viewer, or null if the room has none.</param>
    public sealed record RoomGallery(int RoomId, IReadOnlyList<RoomPhoto> Photos, RoomPhoto? Panorama)
    {
        /// <summary>True if the gallery meets AC1 (at least 8 photos).</summary>
        public bool MeetsMinimumPhotoCount => Photos.Count >= PhotoGalleryLimits.MinRecommendedPhotos;
    }

    /// <summary>
    /// One photo of a room. Load <see cref="ThumbnailSource"/> first, then <see cref="OriginalSource"/>
    /// (progressive loading, AC3).
    /// </summary>
    /// <param name="Key">Value stored in PhotosJson / Panorama360Url; pass it back to Remove/Reorder.</param>
    /// <param name="OriginalSource">Full-size image: local file path, bundled image name, or URL.</param>
    /// <param name="ThumbnailSource">Small preview; equals OriginalSource when no thumbnail exists (seed/remote images).</param>
    /// <param name="IsPanorama">True for the 360° photo, so the client opens the interactive viewer (AC2).</param>
    /// <param name="SortOrder">0-based display order; the panorama uses -1.</param>
    public sealed record RoomPhoto(
        string Key, string OriginalSource, string ThumbnailSource, bool IsPanorama, int SortOrder);

    /// <summary>Why a photo operation failed (comments show the equivalent HTTP status).</summary>
    public enum PhotoErrorCode
    {
        None = 0,
        InvalidInput,     // 400
        NotSignedIn,      // 401
        Forbidden,        // 403
        RoomNotFound,     // 404 (also used for an unknown photo key)
        FileTooLarge,     // 413
        UnsupportedType,  // 415
        LimitReached,     // 409/400: room already has MaxPhotosPerRoom photos
        StorageFailed,    // 500: disk or database error, details are logged
    }

    /// <summary>Result of an upload/remove/reorder, with a Vietnamese message ready for the UI.</summary>
    public sealed record PhotoOperationResult(bool Succeeded, PhotoErrorCode ErrorCode, string Message, RoomPhoto? Photo = null)
    {
        public static PhotoOperationResult Success(string message, RoomPhoto? photo = null) =>
            new(true, PhotoErrorCode.None, message, photo);

        public static PhotoOperationResult Failure(PhotoErrorCode errorCode, string message) =>
            new(false, errorCode, message);
    }
}
