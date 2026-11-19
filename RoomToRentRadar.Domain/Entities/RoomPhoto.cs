using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// An uploaded listing photo (<c>docs/erd.md</c> §4.8, BR-MED-01..05). Unattached photos keep
/// <see cref="RoomId"/> null until the create/update listing flow attaches them; the 24 h cleanup
/// window of BR-MED-03 is applied by the repository query, not by this entity.
/// </summary>
public sealed class RoomPhoto
{
    public const string JpegContentType = "image/jpeg";
    public const string PngContentType = "image/png";

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

    private RoomPhoto(
        string id,
        string? roomId,
        string uploadedByUserId,
        PhotoKind kind,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        string originalPath,
        string mediumPath,
        string thumbPath,
        int sortOrder,
        DateTime createdAtUtc,
        DateTime? attachedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Photo Id is required.");
        }

        if (string.IsNullOrWhiteSpace(uploadedByUserId))
        {
            throw new DomainValidationException("Photo uploader is required.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainValidationException($"Photo kind '{(int)kind}' is not a recognized photo kind.");
        }

        var normalizedContentType = contentType?.Trim() ?? string.Empty;
        if (!string.Equals(normalizedContentType, JpegContentType, StringComparison.Ordinal) &&
            !string.Equals(normalizedContentType, PngContentType, StringComparison.Ordinal))
        {
            throw new DomainValidationException(
                $"Photo content type '{contentType}' is not supported; only {JpegContentType} and {PngContentType} are allowed."); // BR-MED-02
        }

        if (sizeBytes <= 0 || sizeBytes > PhotoFileRules.MaxFileBytes)
        {
            throw new DomainValidationException(
                $"Photo size must be between 1 and {PhotoFileRules.MaxFileBytes:N0} bytes."); // BR-MED-02
        }

        if (width <= 0 || height <= 0)
        {
            throw new DomainValidationException("Photo width and height must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(originalPath))
        {
            throw new DomainValidationException("Photo original path is required.");
        }

        if (string.IsNullOrWhiteSpace(mediumPath))
        {
            throw new DomainValidationException("Photo medium path is required.");
        }

        if (string.IsNullOrWhiteSpace(thumbPath))
        {
            throw new DomainValidationException("Photo thumb path is required.");
        }

        if (sortOrder < 0)
        {
            throw new DomainValidationException("Photo sort order must be zero or greater.");
        }

        Id = id.Trim();
        RoomId = string.IsNullOrWhiteSpace(roomId) ? null : roomId.Trim();
        UploadedByUserId = uploadedByUserId.Trim();
        Kind = kind;
        ContentType = normalizedContentType;
        SizeBytes = sizeBytes;
        Width = width;
        Height = height;
        OriginalPath = originalPath.Trim();
        MediumPath = mediumPath.Trim();
        ThumbPath = thumbPath.Trim();
        SortOrder = sortOrder;
        CreatedAtUtc = AsUtc(createdAtUtc)!.Value;
        AttachedAtUtc = AsUtc(attachedAtUtc);
    }

    /// <summary>Creates a freshly uploaded, still unattached photo (BR-MED-03: expires after 24 h).</summary>
    public static RoomPhoto Create(
        string uploadedByUserId,
        PhotoKind kind,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        string originalPath,
        string mediumPath,
        string thumbPath,
        DateTime? utcNow = null)
    {
        return new RoomPhoto(
            id: Guid.NewGuid().ToString("N"),
            roomId: null,
            uploadedByUserId: uploadedByUserId,
            kind: kind,
            contentType: contentType,
            sizeBytes: sizeBytes,
            width: width,
            height: height,
            originalPath: originalPath,
            mediumPath: mediumPath,
            thumbPath: thumbPath,
            sortOrder: 0,
            createdAtUtc: utcNow ?? DateTime.UtcNow,
            attachedAtUtc: null);
    }

    /// <summary>Rebuilds a photo from persistence or a test fixture; every column of ERD §4.8 is accepted.</summary>
    public static RoomPhoto Reconstitute(
        string id,
        string? roomId,
        string uploadedByUserId,
        PhotoKind kind,
        string contentType,
        long sizeBytes,
        int width,
        int height,
        string originalPath,
        string mediumPath,
        string thumbPath,
        int sortOrder,
        DateTime createdAtUtc,
        DateTime? attachedAtUtc = null)
    {
        return new RoomPhoto(
            id,
            roomId,
            uploadedByUserId,
            kind,
            contentType,
            sizeBytes,
            width,
            height,
            originalPath,
            mediumPath,
            thumbPath,
            sortOrder,
            createdAtUtc,
            attachedAtUtc);
    }

    /// <summary>ERD §1: every date-time column is stored and exposed as UTC.</summary>
    private static DateTime? AsUtc(DateTime? value)
    {
        if (value is not { } instant)
        {
            return null;
        }

        return instant.Kind switch
        {
            DateTimeKind.Utc => instant,
            DateTimeKind.Local => instant.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instant, DateTimeKind.Utc)
        };
    }
}
