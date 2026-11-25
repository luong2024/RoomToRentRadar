using System.Threading;
using System.Threading.Tasks;
using Refit;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

public interface IFavouritesApiService
{
    [Get("/api/v1/favourites")]
    Task<FavouritesListResponse> GetFavouritesAsync(CancellationToken cancellationToken = default);

    [Post("/api/v1/favourites/{listingId}")]
    Task<FavouriteToggleResponse> AddFavouriteAsync(string listingId, CancellationToken cancellationToken = default);

    [Delete("/api/v1/favourites/{listingId}")]
    Task<FavouriteToggleResponse> RemoveFavouriteAsync(string listingId, CancellationToken cancellationToken = default);
}