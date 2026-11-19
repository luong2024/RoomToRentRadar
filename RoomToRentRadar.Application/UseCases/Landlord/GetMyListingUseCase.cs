using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// <c>operationId: getMyListing</c> (UC-31): ownership-scoped read with the full editable payload,
/// the ordered photos (sortOrder 0 = cover) and the current house-rules version. Another landlord's
/// room is a 404 <c>ROOM_NOT_FOUND</c> (anti-IDOR, GUARDRAILS §1.5.6).
/// </summary>
public sealed class GetMyListingUseCase : IGetMyListingUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IPhotoStoragePort _storage;

    public GetMyListingUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(roomRepository);
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(storage);

        _roomRepository = roomRepository;
        _photoRepository = photoRepository;
        _storage = storage;
    }

    public async Task<MyListingDetailDto> ExecuteAsync(
        string landlordId,
        string roomId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new DomainValidationException("The landlord is required.");
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new DomainValidationException("Room id is required.");
        }

        var room = await _roomRepository
            .GetOwnedByIdAsync(roomId, landlordId, ct)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(
                ProblemCodes.RoomNotFound,
                $"Room '{roomId}' was not found.");

        var photos = await _photoRepository.ListByRoomAsync(room.Id, ct).ConfigureAwait(false);
        return LandlordListingMapper.ToDetail(room, photos, _storage);
    }
}
