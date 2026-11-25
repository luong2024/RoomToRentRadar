namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

public sealed class GetListingCoordinatesUseCase(IRoomRepository roomRepository) : IGetListingCoordinatesUseCase
{
    public async Task<ListingCoordinatesResult> ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty || string.IsNullOrWhiteSpace(listingId))
        {
            throw new NotFoundException("Listing was not found.");
        }

        var room = await roomRepository.GetByIdAsync(listingId, cancellationToken);
        if (room is null || room.LandlordId != ownerId)
        {
            throw new NotFoundException("Listing was not found.");
        }

        return new ListingCoordinatesResult(room.Id, room.GetLocation());
    }
}
