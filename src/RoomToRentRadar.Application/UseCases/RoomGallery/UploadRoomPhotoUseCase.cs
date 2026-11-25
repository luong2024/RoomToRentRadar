using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

public sealed class UploadRoomPhotoUseCase : IUploadRoomPhotoUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IImageCompressionService _compressionService;
    private readonly IRoomPhotoStorageService _storageService;
    private readonly TimeProvider _timeProvider;

    public UploadRoomPhotoUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IImageCompressionService compressionService,
        IRoomPhotoStorageService storageService,
        TimeProvider? timeProvider = null)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RoomPhotoDto> ExecuteAsync(
        UploadRoomPhotoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new InvalidRoomPhotoException("Room ID is required.");
        }

        if (command.UploadedByUserId == Guid.Empty)
        {
            throw new InvalidRoomPhotoException("Uploader user ID is required.");
        }

        if (command.FileBytes is null || command.FileBytes.Length == 0)
        {
            throw new InvalidRoomPhotoException("Photo file cannot be empty.");
        }

        if (command.FileBytes.Length > RoomPhoto.MaxFileSizeBytes)
        {
            throw new InvalidRoomPhotoException(
                $"Photo file exceeds the maximum allowed size of {RoomPhoto.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var normalizedRoomId = command.RoomId.Trim();
        var room = await _roomRepository.GetByIdAsync(normalizedRoomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Room '{normalizedRoomId}' was not found.");
        }

        RoomGalleryAuthorization.EnsureCanManageGallery(room, command.UploadedByUserId, command.IsAdmin);

        var canonicalRoomId = room.Id;

        var existingCount = await _photoRepository.CountByRoomIdAsync(canonicalRoomId, cancellationToken);
        if (existingCount >= RoomPhoto.MaxPhotosPerRoom)
        {
            throw new RoomPhotoQuotaExceededException(
                $"Room '{canonicalRoomId}' has reached the maximum limit of {RoomPhoto.MaxPhotosPerRoom} photos.");
        }

        var processedImage = await _compressionService.ProcessAsync(
            command.FileBytes,
            command.ContentType,
            command.Is360,
            cancellationToken);

        var photoId = $"photo-{Guid.NewGuid():N}";
        var storedPhoto = await _storageService.UploadAsync(
            canonicalRoomId,
            photoId,
            processedImage,
            cancellationToken);

        RoomPhoto newPhoto;
        try
        {
            newPhoto = await _photoRepository.AddWithinQuotaAsync(
                canonicalRoomId,
                RoomPhoto.MaxPhotosPerRoom,
                command.IsCover,
                (displayOrder, isCover) => RoomPhoto.Create(
                    id: photoId,
                    roomId: canonicalRoomId,
                    url: storedPhoto.Url,
                    thumbnailUrl: storedPhoto.ThumbnailUrl,
                    objectKey: storedPhoto.ObjectKey,
                    thumbnailObjectKey: storedPhoto.ThumbnailObjectKey,
                    contentType: processedImage.ContentType,
                    sizeBytes: processedImage.CompressedBytes.LongLength,
                    width: processedImage.Width,
                    height: processedImage.Height,
                    is360: command.Is360,
                    isCover: isCover,
                    displayOrder: displayOrder,
                    caption: command.Caption,
                    uploadedByUserId: command.UploadedByUserId,
                    uploadedAtUtc: _timeProvider.GetUtcNow()),
                cancellationToken);
        }
        catch
        {
            try
            {
                await _storageService.DeleteAsync(
                    storedPhoto.ObjectKey,
                    storedPhoto.ThumbnailObjectKey,
                    CancellationToken.None);
            }
            catch
            {
                // Preserve original persistence exception when rollback cleanup fails.
            }

            throw;
        }

        return RoomGalleryMapper.ToDto(newPhoto);
    }
}
