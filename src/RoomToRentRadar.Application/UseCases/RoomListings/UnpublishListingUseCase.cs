namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

public sealed class UnpublishListingUseCase : IUnpublishListingUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomListingVisibilityWriter _visibilityWriter;
    private readonly IRoomListingCacheInvalidator _cacheInvalidator;

    public UnpublishListingUseCase(
        IRoomRepository roomRepository,
        IRoomListingVisibilityWriter visibilityWriter,
        IRoomListingCacheInvalidator cacheInvalidator)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _visibilityWriter = visibilityWriter ?? throw new ArgumentNullException(nameof(visibilityWriter));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
    }

    public async Task ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty) throw new RequestValidationException("ownerId", "A valid landlord identity is required.");
        if (string.IsNullOrWhiteSpace(listingId)) throw new NotFoundException("Listing was not found.");
        var room = await _roomRepository.GetByIdAsync(listingId, cancellationToken);
        if (room is null || room.LandlordId != ownerId)
        {
            throw new NotFoundException("Listing was not found.");
        }

        if (room.Status != RoomStatus.Pending || room.IsVerified)
        {
            room.Status = RoomStatus.Pending;
            room.IsVerified = false;
            room.LastUpdatedDate = DateTime.UtcNow;
            await _roomRepository.UpdateAsync(room, cancellationToken);
        }

        // Finish updating public visibility after the save, even if the client disconnected.
        var campusIds = await _visibilityWriter.HideAsync(room.Id, CancellationToken.None);
        foreach (var campusId in campusIds)
        {
            await _cacheInvalidator.InvalidateCampusAsync(campusId, CancellationToken.None);
        }
    }
}
