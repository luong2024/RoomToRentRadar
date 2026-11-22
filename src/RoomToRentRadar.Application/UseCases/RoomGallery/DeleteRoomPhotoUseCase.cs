using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

public sealed class DeleteRoomPhotoUseCase : IDeleteRoomPhotoUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IRoomPhotoStorageService _storageService;

    public DeleteRoomPhotoUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IRoomPhotoStorageService storageService)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public async Task ExecuteAsync(
        string roomId,
        string photoId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new InvalidRoomPhotoException("Room ID is required.");
        }

        if (string.IsNullOrWhiteSpace(photoId))
        {
            throw new InvalidRoomPhotoException("Photo ID is required.");
        }

        var normalizedRoomId = roomId.Trim();
        var normalizedPhotoId = photoId.Trim();

        var room = await _roomRepository.GetByIdAsync(normalizedRoomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Room '{normalizedRoomId}' was not found.");
        }

        var targetPhoto = await _photoRepository.GetByIdAsync(normalizedRoomId, normalizedPhotoId, cancellationToken);
        if (targetPhoto is null)
        {
            throw new RoomPhotoNotFoundException($"Photo '{normalizedPhotoId}' was not found in room '{normalizedRoomId}'.");
        }

        await _photoRepository.DeleteAsync(normalizedRoomId, normalizedPhotoId, cancellationToken);
        await _storageService.DeleteAsync(targetPhoto.ObjectKey, targetPhoto.ThumbnailObjectKey, cancellationToken);

        if (targetPhoto.IsCover)
        {
            var remaining = await _photoRepository.GetByRoomIdAsync(normalizedRoomId, cancellationToken);
            var nextCover = remaining
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.UploadedAtUtc)
                .FirstOrDefault();

            if (nextCover is not null && !nextCover.IsCover)
            {
                nextCover.SetAsCover(true);
                await _photoRepository.UpdateAsync(nextCover, cancellationToken);
            }
        }
    }
}
