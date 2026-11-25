using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

public sealed class GetRoomGalleryUseCase : IGetRoomGalleryUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;

    public GetRoomGalleryUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
    }

    public async Task<RoomGalleryResponse> ExecuteAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new InvalidRoomPhotoException("Room ID is required.");
        }

        var normalizedRoomId = roomId.Trim();
        var room = await _roomRepository.GetByIdAsync(normalizedRoomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Room '{normalizedRoomId}' was not found.");
        }

        var canonicalRoomId = room.Id;

        // Keep public gallery visibility aligned with GetRoomDetailsUseCase:
        // hide room photos whenever the room is no longer Available.
        if (room.Status != RoomStatus.Available)
        {
            return RoomGalleryMapper.ToGalleryResponse(canonicalRoomId, Array.Empty<RoomPhoto>());
        }

        var photos = await _photoRepository.GetByRoomIdAsync(canonicalRoomId, cancellationToken);
        return RoomGalleryMapper.ToGalleryResponse(canonicalRoomId, photos);
    }
}
