namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

public sealed class UnpublishListingUseCase : IUnpublishListingUseCase
{
    private readonly IRoomRepository _roomRepository;

    public UnpublishListingUseCase(IRoomRepository roomRepository) => _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));

    public async Task ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty) throw new RequestValidationException("ownerId", "A valid landlord identity is required.");
        if (string.IsNullOrWhiteSpace(listingId)) throw new NotFoundException("Listing was not found.");
        var room = await _roomRepository.GetByIdAsync(listingId, cancellationToken);
        if (room is null || !Guid.TryParse(room.LandlordId, out var storedOwnerId) || storedOwnerId != ownerId)
        {
            throw new NotFoundException("Listing was not found.");
        }

        if (room.Status == RoomStatus.Pending) return;
        room.Status = RoomStatus.Pending;
        room.IsVerified = false;
        room.LastUpdatedDate = DateTime.UtcNow;
        await _roomRepository.UpdateAsync(room, cancellationToken);
    }
}
