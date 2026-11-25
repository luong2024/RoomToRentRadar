using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Represents a single photo (standard or 360° panorama) belonging to a room gallery (US03 / T03.1).
/// </summary>
public sealed class RoomPhoto
{
    public const int MaxPhotosPerRoom = 12;
    public const int MinRecommendedSupportedPhotos = 8;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const int MaxCaptionLength = 200;

    public string Id { get; private set; }
    public string RoomId { get; private set; }
    public string Url { get; private set; }
    public string ThumbnailUrl { get; private set; }
    public string ObjectKey { get; private set; }
    public string ThumbnailObjectKey { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool Is360 { get; private set; }
    public bool IsCover { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? Caption { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private RoomPhoto(
        string id,
        string roomId,
        string url,
        string thumbnailUrl,
        string objectKey,
        string thumbnailObjectKey,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        bool is360,
        bool isCover,
        int displayOrder,
        string? caption,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        Id = id;
        RoomId = roomId;
        Url = url;
        ThumbnailUrl = thumbnailUrl;
        ObjectKey = objectKey;
        ThumbnailObjectKey = thumbnailObjectKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Width = width;
        Height = height;
        Is360 = is360;
        IsCover = isCover;
        DisplayOrder = displayOrder;
        Caption = caption;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }

    public static RoomPhoto Create(
        string id,
        string roomId,
        string url,
        string thumbnailUrl,
        string objectKey,
        string thumbnailObjectKey,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        bool is360,
        bool isCover,
        int displayOrder,
        string? caption,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidRoomPhotoException("Photo ID is required.");

        if (string.IsNullOrWhiteSpace(roomId))
            throw new InvalidRoomPhotoException("Room ID is required.");

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidRoomPhotoException("Photo URL is required.");

        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            throw new InvalidRoomPhotoException("Thumbnail URL is required.");

        if (string.IsNullOrWhiteSpace(objectKey))
            throw new InvalidRoomPhotoException("Storage object key is required.");

        if (string.IsNullOrWhiteSpace(thumbnailObjectKey))
            throw new InvalidRoomPhotoException("Thumbnail storage object key is required.");

        if (string.IsNullOrWhiteSpace(contentType))
            throw new InvalidRoomPhotoException("Content type is required.");

        if (sizeBytes <= 0 || sizeBytes > MaxFileSizeBytes)
            throw new InvalidRoomPhotoException($"Photo size must be between 1 byte and {MaxFileSizeBytes / (1024 * 1024)} MB.");

        if (width <= 0 || height <= 0)
            throw new InvalidRoomPhotoException("Photo dimensions must be positive.");

        if (displayOrder < 0)
            throw new InvalidRoomPhotoException("Display order cannot be negative.");

        if (uploadedByUserId == Guid.Empty)
            throw new InvalidRoomPhotoException("Uploader user ID is required.");

        var normalizedCaption = NormalizeCaption(caption);

        return new RoomPhoto(
            id.Trim(),
            roomId.Trim(),
            url.Trim(),
            thumbnailUrl.Trim(),
            objectKey.Trim(),
            thumbnailObjectKey.Trim(),
            contentType.Trim().ToLowerInvariant(),
            sizeBytes,
            width,
            height,
            is360,
            isCover,
            displayOrder,
            normalizedCaption,
            uploadedByUserId,
            uploadedAtUtc);
    }

    public static RoomPhoto Reconstitute(
        string id,
        string roomId,
        string url,
        string thumbnailUrl,
        string objectKey,
        string thumbnailObjectKey,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        bool is360,
        bool isCover,
        int displayOrder,
        string? caption,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        return new RoomPhoto(
            id,
            roomId,
            url,
            thumbnailUrl,
            objectKey,
            thumbnailObjectKey,
            contentType,
            sizeBytes,
            width,
            height,
            is360,
            isCover,
            displayOrder,
            caption,
            uploadedByUserId,
            uploadedAtUtc);
    }

    public void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new InvalidRoomPhotoException("Display order cannot be negative.");

        DisplayOrder = displayOrder;
    }

    public void SetAsCover(bool isCover)
    {
        IsCover = isCover;
    }

    private static string? NormalizeCaption(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return null;
        }

        var trimmed = caption.Trim();
        if (trimmed.Length > MaxCaptionLength)
        {
            throw new InvalidRoomPhotoException($"Caption cannot exceed {MaxCaptionLength} characters.");
        }

        return trimmed;
    }
}
