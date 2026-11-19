using System.Globalization;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

/// <summary>
/// <c>operationId: uploadPhoto</c> (US08 AC4 / UC-30, BR-MED-01..04): validate size and magic bytes,
/// read the pixel size, check the 2:1 panorama ratio, strip EXIF, then store the file and the
/// <see cref="RoomPhoto"/> row. A failed INSERT removes the just-written file again.
/// </summary>
/// <remarks>
/// The zero-NuGet decision (implementation_plan §1.3 / ADR-008) rules out re-encoding the image, so no
/// 400 px / 1280 px renditions are generated: the stripped original is stored once and
/// <see cref="RoomPhoto.MediumPath"/>/<see cref="RoomPhoto.ThumbPath"/> point at the same key. The
/// three URL fields therefore resolve to the same file until a resizer task exists.
/// </remarks>
public sealed class UploadPhotoUseCase : IUploadPhotoUseCase
{
    private const int MagicByteCount = 8;

    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IPhotoStoragePort _storage;
    private readonly IImageProcessorPort _imageProcessor;

    public UploadPhotoUseCase(
        IRoomPhotoRepository photoRepository,
        IPhotoStoragePort storage,
        IImageProcessorPort imageProcessor)
    {
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(imageProcessor);

        _photoRepository = photoRepository;
        _storage = storage;
        _imageProcessor = imageProcessor;
    }

    public async Task<PhotoUploadDto> ExecuteAsync(UploadPhotoRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new DomainValidationException("The uploader is required.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            throw new DomainValidationException($"Photo kind '{(int)request.Kind}' is not a recognized photo kind.");
        }

        // BR-MED-02: an empty upload is a validation error (400); anything above 10 MB is 413.
        if (request.Length <= 0)
        {
            throw new DomainValidationException("The uploaded file is empty.", ProblemCodes.ValidationFailed);
        }

        if (request.Length > PhotoFileRules.MaxFileBytes)
        {
            throw new FileTooLargeException();
        }

        var content = request.Content
            ?? throw new DomainValidationException("Photo content is required.");

        // Buffer once so the magic-byte, header and strip steps all read a seekable copy; the copy never
        // reads more than the declared length (a stream longer than declared is rejected as invalid).
        await using var buffered = await CopyBoundedAsync(content, request.Length, ct).ConfigureAwait(false);

        var header = new byte[MagicByteCount];
        var headerLength = buffered.Read(header, 0, header.Length);
        buffered.Position = 0;
        var extension = PhotoFileRules.DetectExtension(header.AsSpan(0, headerLength));
        if (extension is null ||
            !ImageHeaderReader.TryReadSize(buffered, out var width, out var height))
        {
            throw new UnsupportedMediaTypeException();
        }

        if (request.Kind == PhotoKind.Panorama && !PhotoFileRules.IsPanoramaRatio(width, height))
        {
            throw new DomainValidationException(
                "Panorama photos must have a 2:1 aspect ratio (±2%).",
                ProblemCodes.ValidationFailed);
        }

        var utcNow = DateTime.UtcNow;
        var photoId = Guid.NewGuid().ToString("N");
        var storageKey = string.Create(
            CultureInfo.InvariantCulture,
            $"rooms/{utcNow:yyyy}/{utcNow:MM}/{photoId}/original{extension}");
        var contentType = extension == ".png" ? RoomPhoto.PngContentType : RoomPhoto.JpegContentType;

        await using var processed = await _imageProcessor
            .ProcessAndStripExifAsync(buffered, extension, ct)
            .ConfigureAwait(false);
        await using var cleaned = await CopyAllAsync(processed, ct).ConfigureAwait(false);
        var sizeBytes = cleaned.Length;
        if (sizeBytes <= 0)
        {
            throw new UnsupportedMediaTypeException("The image could not be processed.");
        }

        if (sizeBytes > PhotoFileRules.MaxFileBytes)
        {
            throw new FileTooLargeException();
        }

        var photo = RoomPhoto.Reconstitute(
            id: photoId,
            roomId: null,
            uploadedByUserId: request.UserId,
            kind: request.Kind,
            contentType: contentType,
            sizeBytes: sizeBytes,
            width: width,
            height: height,
            originalPath: storageKey,
            mediumPath: storageKey,
            thumbPath: storageKey,
            sortOrder: 0,
            createdAtUtc: utcNow);

        try
        {
            await _storage.SaveAsync(storageKey, cleaned, ct).ConfigureAwait(false);
            await _photoRepository.AddAsync(photo, ct).ConfigureAwait(false);
        }
        catch
        {
            // BR-MED-03: never keep a file whose row failed to persist.
            await DeleteBestEffortAsync(storageKey).ConfigureAwait(false);
            throw;
        }

        return new PhotoUploadDto(
            Id: photo.Id,
            Kind: photo.Kind,
            ThumbUrl: _storage.GetPublicUrl(photo.ThumbPath),
            MediumUrl: _storage.GetPublicUrl(photo.MediumPath),
            Width: photo.Width,
            Height: photo.Height,
            SizeBytes: photo.SizeBytes);
    }

    private static async Task<MemoryStream> CopyBoundedAsync(
        Stream source,
        long declaredLength,
        CancellationToken ct)
    {
        var buffer = new MemoryStream((int)Math.Min(declaredLength, 81920));
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > PhotoFileRules.MaxFileBytes)
            {
                throw new FileTooLargeException();
            }

            if (total > declaredLength)
            {
                throw new DomainValidationException(
                    "The uploaded file is larger than its declared length.",
                    ProblemCodes.ValidationFailed);
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static async Task<MemoryStream> CopyAllAsync(Stream source, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }

    private async Task DeleteBestEffortAsync(string storageKey)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The INSERT already failed; an orphaned file is cleaned up by the next media audit.
        }
    }
}
