using RoomToRentRadar.Models.RoomListings;

namespace RoomToRentRadar.Services.RoomListings;

public interface IFavouritesService
{
    Task<IReadOnlyList<RoomListItemModel>> GetMyFavouritesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetMyFavouriteIdsAsync(CancellationToken cancellationToken = default);
    Task AddFavouriteAsync(string roomId, CancellationToken cancellationToken = default);
    Task RemoveFavouriteAsync(string roomId, CancellationToken cancellationToken = default);
}
