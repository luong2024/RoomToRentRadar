using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

public sealed class UploadPhotoUseCase : IUploadPhotoUseCase
{
    private const int HeaderLength = 16;

    private readonly IRoomPhotoRepository _repository;
    private readonly IPhotoStoragePort _storage;

    public UploadPhotoUseCase(IRoomPhotoRepository repository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);

        _repository = repository;
        _storage = storage;
    }

    public async Task<PhotoUploadDto> ExecuteAsync(UploadPhotoRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new DomainValidationException("User id is required.");
        }

        // GUARDRAILS 1.5.1: whitelist the enum so unknown values cannot reach storage or the database.
        if (!Enum.IsDefined(request.Kind))
        {
            throw new DomainValidationException("Photo kind is invalid.");
        }

        if (request.Content is null)
        {
            throw new DomainValidationException("Photo file is required.");
        }

        if (request.Length <= 0)
        {
            throw new DomainValidationException("Photo file is empty.");
        }

        if (request.Length > PhotoFileRules.MaxFileBytes)
        {
            throw new FileTooLargeException(PhotoFileRules.MaxFileBytes);
        }

        if (!request.Content.CanSeek)
        {
            throw new DomainValidationException("Photo stream must be seekable.");
        }

        var header = new byte[HeaderLength];
        request.Content.Position = 0;
        var bytesRead = request.Content.Read(header, 0, header.Length);
        request.Content.Position = 0;

        var extension = PhotoFileRules.DetectExtension(header.AsSpan(0, bytesRead))
            ?? throw new UnsupportedMediaTypeException("File is not a valid JPEG or PNG image.");

        var size = ImageHeaderReader.TryReadSize(request.Content)
            ?? throw new UnsupportedMediaTypeException("File is not a valid JPEG or PNG image.");
        request.Content.Position = 0;

        if (request.Kind == PhotoKind.Panorama && !PhotoFileRules.IsPanoramaRatio(size.Width, size.Height))
        {
            throw new DomainValidationException("Panorama photos must use a 2:1 aspect ratio (±2%).");
        }

        var id = Guid.NewGuid().ToString("N");
        var createdAtUtc = DateTime.UtcNow;
        var storageKey = $"rooms/{createdAtUtc:yyyy}/{createdAtUtc:MM}/{id}/original.{extension}";
        var contentType = PhotoFileRules.ContentTypeOf(extension)!;

        await _storage.SaveAsync(storageKey, request.Content, ct);

        var photo = new RoomPhoto(
            uploadedByUserId: request.UserId,
            kind: request.Kind,
            contentType: contentType,
            sizeBytes: request.Length,
            width: size.Width,
            height: size.Height,
            originalPath: storageKey,
            mediumPath: storageKey,
            thumbPath: storageKey,
            id: id,
            createdAtUtc: createdAtUtc);

        try
        {
            await _repository.AddAsync(photo, ct);
        }
        catch
        {
            // Best effort: remove the orphaned file, then surface the original failure.
            try
            {
                await _storage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch
            {
                // Storage cleanup must never hide the metadata failure.
            }

            throw;
        }

        // Thumb/medium renditions arrive in T03.1b; until then all URLs point to the original.
        var url = _storage.GetPublicUrl(storageKey);
        return new PhotoUploadDto(photo.Id, photo.Kind, url, url, photo.Width, photo.Height, photo.SizeBytes);
    }
}
