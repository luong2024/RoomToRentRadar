using System.Collections.Concurrent;
using System.Text;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Adapters.Outbound.Storage;

/// <summary>
/// In-memory object storage fallback for Development and CI testing when Cloudflare R2 keys are not configured.
/// </summary>
public sealed class InMemoryRoomPhotoStorageService : IRoomPhotoStorageService
{
    private const string DefaultPublicBaseUrl = "https://in-memory.roomtorentradar.local";

    private readonly ConcurrentDictionary<string, StoredBlobEntry> _objects = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, StoredBlobEntry> Objects => _objects;

    public Task<StoredPhotoResult> UploadAsync(
        string roomId,
        string photoId,
        ProcessedImageResult processedImage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);
        ArgumentNullException.ThrowIfNull(processedImage);

        var safeRoom = SanitizeSegment(roomId);
        var safePhoto = SanitizeSegment(photoId);
        var extension = processedImage.FileExtension.StartsWith('.')
            ? processedImage.FileExtension
            : "." + processedImage.FileExtension;

        var objectKey = $"rooms/{safeRoom}/{safePhoto}{extension}";
        var thumbnailKey = $"rooms/{safeRoom}/{safePhoto}_thumb{extension}";

        _objects[objectKey] = new StoredBlobEntry(processedImage.CompressedBytes.ToArray(), processedImage.ContentType);
        _objects[thumbnailKey] = new StoredBlobEntry(processedImage.ThumbnailBytes.ToArray(), processedImage.ContentType);

        return Task.FromResult(new StoredPhotoResult(
            Url: $"{DefaultPublicBaseUrl}/{objectKey}",
            ThumbnailUrl: $"{DefaultPublicBaseUrl}/{thumbnailKey}",
            ObjectKey: objectKey,
            ThumbnailObjectKey: thumbnailKey));
    }

    public Task DeleteAsync(
        string objectKey,
        string thumbnailObjectKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            _objects.TryRemove(objectKey, out _);
        }

        if (!string.IsNullOrWhiteSpace(thumbnailObjectKey))
        {
            _objects.TryRemove(thumbnailObjectKey, out _);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeSegment(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
            {
                sb.Append(ch);
            }
        }

        return sb.Length > 0 ? sb.ToString() : "item";
    }

    public sealed record StoredBlobEntry(byte[] Bytes, string ContentType);
}
