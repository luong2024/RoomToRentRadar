using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

public sealed class ReorderRoomPhotosUseCase : IReorderRoomPhotosUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;

    public ReorderRoomPhotosUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
    }

    public async Task<RoomGalleryResponse> ExecuteAsync(
        ReorderRoomPhotosCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new InvalidRoomPhotoException("Room ID is required.");
        }

        var normalizedRoomId = command.RoomId.Trim();
        var room = await _roomRepository.GetByIdAsync(normalizedRoomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Room '{normalizedRoomId}' was not found.");
        }

        var existingPhotos = await _photoRepository.GetByRoomIdAsync(normalizedRoomId, cancellationToken);
        var photosById = existingPhotos.ToDictionary(p => p.Id, StringComparer.Ordinal);

        var orderedIds = (command.OrderedPhotoIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        if (orderedIds.Count > 0)
        {
            if (orderedIds.Distinct(StringComparer.Ordinal).Count() != orderedIds.Count)
            {
                throw new InvalidRoomPhotoException("Ordered photo IDs cannot contain duplicates.");
            }

            foreach (var photoId in orderedIds)
            {
                if (!photosById.ContainsKey(photoId))
                {
                    throw new RoomPhotoNotFoundException($"Photo '{photoId}' was not found in room '{normalizedRoomId}'.");
                }
            }

            var nextOrder = 0;
            foreach (var photoId in orderedIds)
            {
                photosById[photoId].SetDisplayOrder(nextOrder++);
            }

            foreach (var remainingPhoto in existingPhotos
                         .Where(p => !orderedIds.Contains(p.Id, StringComparer.Ordinal))
                         .OrderBy(p => p.DisplayOrder))
            {
                remainingPhoto.SetDisplayOrder(nextOrder++);
            }
        }

        if (!string.IsNullOrWhiteSpace(command.CoverPhotoId))
        {
            var coverId = command.CoverPhotoId.Trim();
            if (!photosById.ContainsKey(coverId))
            {
                throw new RoomPhotoNotFoundException($"Cover photo '{coverId}' was not found in room '{normalizedRoomId}'.");
            }

            foreach (var photo in existingPhotos)
            {
                photo.SetAsCover(string.Equals(photo.Id, coverId, StringComparison.Ordinal));
            }
        }

        foreach (var photo in existingPhotos)
        {
            await _photoRepository.UpdateAsync(photo, cancellationToken);
        }

        return RoomGalleryMapper.ToGalleryResponse(normalizedRoomId, existingPhotos);
    }
}
