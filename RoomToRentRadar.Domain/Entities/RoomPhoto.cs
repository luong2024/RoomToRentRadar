using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

public sealed class RoomPhoto
{
    public string Id { get; }
    public string RoomId { get; }
    public string FileName { get; }
    public string Url { get; }
    public string ContentType { get; }
    public long SizeBytes { get; }
    public string UploadedByUserId { get; }
    public DateTime UploadedAtUtc { get; }

    public RoomPhoto(
        string roomId,
        string fileName,
        string url,
        string contentType,
        long sizeBytes,
        string uploadedByUserId,
        string? id = null,
        DateTime? uploadedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new DomainValidationException("Room id is required.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainValidationException("File name is required.");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new DomainValidationException("Photo URL is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new DomainValidationException("Content type is required.");
        }

        if (sizeBytes < 0)
        {
            throw new DomainValidationException("Photo size cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(uploadedByUserId))
        {
            throw new DomainValidationException("Uploader id is required.");
        }

        if (id is not null && string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Photo id cannot be empty when provided.");
        }

        var resolvedUploadedAtUtc = uploadedAtUtc ?? DateTime.UtcNow;

        Id = id ?? Guid.NewGuid().ToString("N");
        RoomId = roomId;
        FileName = fileName;
        Url = url;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = resolvedUploadedAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(resolvedUploadedAtUtc, DateTimeKind.Utc)
            : resolvedUploadedAtUtc.ToUniversalTime();
    }
}
