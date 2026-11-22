namespace RoomToRentRadar.Application.DTOs;

public sealed record RoomPhotoDto(
    string Id,
    string RoomId,
    string Url,
    string ThumbnailUrl,
    bool Is360,
    bool IsCover,
    int DisplayOrder,
    int Width,
    int Height,
    long SizeBytes,
    string ContentType,
    string? Caption,
    DateTimeOffset UploadedAtUtc);

public sealed record RoomGalleryResponse(
    string RoomId,
    int TotalCount,
    bool HasPhotos,
    bool Has360Photos,
    IReadOnlyList<RoomPhotoDto> Photos);

public sealed record UploadRoomPhotoCommand(
    string RoomId,
    Guid UploadedByUserId,
    string FileName,
    string ContentType,
    byte[] FileBytes,
    bool Is360 = false,
    bool IsCover = false,
    string? Caption = null);

public sealed record ReorderRoomPhotosCommand(
    string RoomId,
    IReadOnlyList<string> OrderedPhotoIds,
    string? CoverPhotoId = null);

public sealed record ProcessedImageResult(
    byte[] CompressedBytes,
    byte[] ThumbnailBytes,
    string ContentType,
    string FileExtension,
    int Width,
    int Height);

public sealed record StoredPhotoResult(
    string Url,
    string ThumbnailUrl,
    string ObjectKey,
    string ThumbnailObjectKey);
