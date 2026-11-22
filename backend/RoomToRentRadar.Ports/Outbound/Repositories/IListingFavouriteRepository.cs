using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IListingFavouriteRepository
{
    Task<IReadOnlyList<FavouriteRoomDto>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid userId, Guid listingId, CancellationToken cancellationToken);

    Task AddAsync(ListingFavourite favourite, CancellationToken cancellationToken);

    Task RemoveAsync(Guid userId, Guid listingId, CancellationToken cancellationToken);
}
