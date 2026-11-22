using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Ports.Inbound;

public interface IFavouritesUseCase
{
    Task<IReadOnlyList<FavouriteRoomDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Returns <c>false</c> when the listing does not exist.</summary>
    Task<bool> AddAsync(Guid userId, Guid listingId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid userId, Guid listingId, CancellationToken cancellationToken);
}
