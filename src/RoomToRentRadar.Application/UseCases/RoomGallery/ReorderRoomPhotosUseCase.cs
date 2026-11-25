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

        RoomGalleryAuthorization.EnsureCanManageGallery(room, command.ActorUserId, command.IsAdmin);

        var canonicalRoomId = room.Id;

        var orderedIds = (command.OrderedPhotoIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        if (orderedIds.Distinct(StringComparer.Ordinal).Count() != orderedIds.Count)
        {
            throw new InvalidRoomPhotoException("Ordered photo IDs cannot contain duplicates.");
        }

        var updatedPhotos = await _photoRepository.ReorderAsync(
            canonicalRoomId,
            orderedIds,
            command.CoverPhotoId,
            cancellationToken);

        return RoomGalleryMapper.ToGalleryResponse(canonicalRoomId, updatedPhotos);
    }
}
