using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

public sealed class UploadRoomPhotosUseCase : IUploadRoomPhotosUseCase
{
    private const int HeaderLength = 8;

    private readonly IRoomPhotoRepository _repository;
    private readonly IPhotoStoragePort _storage;

    public UploadRoomPhotosUseCase(IRoomPhotoRepository repository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);

        _repository = repository;
        _storage = storage;
    }

    public async Task<IReadOnlyList<RoomPhotoDto>> ExecuteAsync(UploadRoomPhotosRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PhotoFileRules.IsValidRoomId(request.RoomId))
        {
            throw new DomainValidationException("Room id is invalid.");
        }

        var files = request.Files;
        if (files is null || files.Count == 0)
        {
            throw new DomainValidationException("At least one photo file is required.");
        }

        var extensions = ValidateFiles(files);

        var existing = await _repository.ListByRoomAsync(request.RoomId, ct);
        if (existing.Count + files.Count > PhotoFileRules.MaxPhotosPerRoom)
        {
            throw new DomainValidationException(
                $"A room can contain at most {PhotoFileRules.MaxPhotosPerRoom} photos.");
        }

        var utcNow = DateTime.UtcNow;
        var created = new List<RoomPhotoDto>(files.Count);

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var extension = extensions[index];
            var fileName = $"{Guid.NewGuid():N}.{extension}";
            var url = await _storage.SaveAsync(request.RoomId, fileName, file.Content, ct);

            var photo = new RoomPhoto
            {
                RoomId = request.RoomId,
                FileName = fileName,
                Url = url,
                ContentType = extension == "png" ? "image/png" : "image/jpeg",
                SizeBytes = file.Length,
                UploadedByUserId = request.UploadedByUserId,
                UploadedAtUtc = utcNow.AddTicks(index)
            };

            try
            {
                await _repository.AddAsync(photo, ct);
            }
            catch
            {
                await _storage.DeleteAsync(request.RoomId, fileName, CancellationToken.None);
                throw;
            }

            created.Add(new RoomPhotoDto(
                Id: photo.Id,
                Url: photo.Url,
                IsCover: existing.Count == 0 && index == 0,
                UploadedAtUtc: photo.UploadedAtUtc));
        }

        return created;
    }

    // Validates every file before any file is persisted so that a single invalid
    // file rejects the whole batch without leaving orphaned files behind.
    private static string[] ValidateFiles(IReadOnlyList<PhotoFileInput> files)
    {
        var extensions = new string[files.Count];
        var header = new byte[HeaderLength];

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            if (file is null)
            {
                throw new DomainValidationException("Photo file is missing.");
            }

            if (file.Length <= 0)
            {
                throw new DomainValidationException($"File '{file.FileName}' is empty.");
            }

            if (file.Length > PhotoFileRules.MaxFileBytes)
            {
                throw new DomainValidationException(
                    $"File '{file.FileName}' exceeds the maximum allowed size of {PhotoFileRules.MaxFileBytes} bytes.");
            }

            file.Content.Position = 0;
            var bytesRead = file.Content.Read(header, 0, header.Length);
            file.Content.Position = 0;

            var extension = PhotoFileRules.DetectExtension(header.AsSpan(0, bytesRead));
            if (extension is null)
            {
                throw new DomainValidationException(
                    $"File '{file.FileName}' is not a valid JPG or PNG image.");
            }

            extensions[index] = extension;
        }

        return extensions;
    }
}
