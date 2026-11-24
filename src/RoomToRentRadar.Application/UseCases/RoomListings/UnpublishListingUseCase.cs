namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

public sealed class UnpublishListingUseCase : IUnpublishListingUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomListingRepository _roomListingRepository;
    private readonly IRoomListingCacheInvalidator _cacheInvalidator;

    public UnpublishListingUseCase(
        IRoomRepository roomRepository,
        IRoomListingRepository roomListingRepository,
        IRoomListingCacheInvalidator cacheInvalidator)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _roomListingRepository = roomListingRepository ?? throw new ArgumentNullException(nameof(roomListingRepository));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
    }

    public async Task ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty) throw new RequestValidationException("ownerId", "A valid landlord identity is required.");
        if (string.IsNullOrWhiteSpace(listingId)) throw new NotFoundException("Listing was not found.");
        var room = await _roomRepository.GetByIdAsync(listingId, cancellationToken);
        if (room is null || !Guid.TryParse(room.LandlordId, out var storedOwnerId) || storedOwnerId != ownerId)
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

        var listing = await _roomListingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null)
        {
            return;
        }

        if (listing.Status != RoomToRentRadar.Domain.Enums.RoomStatus.Hidden ||
            listing.VerificationStatus != RoomToRentRadar.Domain.Enums.VerificationStatus.Unverified)
        {
            listing.UpdateStatus(RoomToRentRadar.Domain.Enums.RoomStatus.Hidden);
            listing.UpdateVerificationStatus(RoomToRentRadar.Domain.Enums.VerificationStatus.Unverified);
            await _roomListingRepository.AddAsync(listing, cancellationToken);
        }

        await _cacheInvalidator.InvalidateCampusAsync(listing.CampusId, cancellationToken);
    }
}
