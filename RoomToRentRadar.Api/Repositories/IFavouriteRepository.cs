using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public interface IFavouriteRepository
{
    Task<UserFavouritesDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserFavouritesDocument> SaveAsync(UserFavouritesDocument document, CancellationToken cancellationToken = default);
}
