using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadar.Domain.Entities;

public sealed class RoomPhoto
{
    public string Id { get; private set; }
    public string? RoomId { get; private set; }
    public string UploadedByUserId { get; private set; }
    public PhotoKind Kind { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string OriginalPath { get; private set; }
    public string MediumPath { get; private set; }
    public string ThumbPath { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AttachedAtUtc { get; private set; }

    public RoomPhoto(
        string uploadedByUserId,
        PhotoKind kind,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        string originalPath,
        string mediumPath,
        string thumbPath,
        string? roomId = null,
        int sortOrder = 0,
        string? id = null,
        DateTime? createdAtUtc = null,
        DateTime? attachedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(uploadedByUserId))
        {
            throw new DomainValidationException("Uploader id is required.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainValidationException("Photo kind is not supported.");
        }

        if (contentType is not ("image/jpeg" or "image/png"))
        {
            throw new DomainValidationException("Content type must be image/jpeg or image/png.");
        }

        if (sizeBytes <= 0 || sizeBytes > PhotoFileRules.MaxFileBytes)
        {
            throw new DomainValidationException("Photo size must be between 1 byte and the maximum allowed.");
        }

        if (width <= 0 || height <= 0)
        {
            throw new DomainValidationException("Photo dimensions must be positive.");
        }

        if (string.IsNullOrWhiteSpace(originalPath)
            || string.IsNullOrWhiteSpace(mediumPath)
            || string.IsNullOrWhiteSpace(thumbPath))
        {
            throw new DomainValidationException("Photo storage paths are required.");
        }

        if (roomId is not null && string.IsNullOrWhiteSpace(roomId))
        {
            throw new DomainValidationException("Room id cannot be empty when provided.");
        }

        if (sortOrder < 0)
        {
            throw new DomainValidationException("Sort order cannot be negative.");
        }

        if (id is not null && string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Photo id cannot be empty when provided.");
        }

        Id = id ?? Guid.NewGuid().ToString("N");
        RoomId = roomId;
        UploadedByUserId = uploadedByUserId;
        Kind = kind;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Width = width;
        Height = height;
        OriginalPath = originalPath;
        MediumPath = mediumPath;
        ThumbPath = thumbPath;
        SortOrder = sortOrder;
        CreatedAtUtc = NormalizeUtc(createdAtUtc ?? DateTime.UtcNow);
        AttachedAtUtc = attachedAtUtc is null ? null : NormalizeUtc(attachedAtUtc.Value);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
