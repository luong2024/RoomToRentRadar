using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Favourites;

public interface IFavouriteService
{
    Task<ToggleFavouriteResponse> ToggleAsync(
        string userId,
        ToggleFavouriteRequest request,
        CancellationToken cancellationToken = default);

    Task<FavouritesListResponse> GetFavouritesAsync(
        string userId,
        bool includeUnavailable = true,
        CancellationToken cancellationToken = default);

    Task<SyncFavouritesResponse> SyncAsync(
        string userId,
        SyncFavouritesRequest request,
        CancellationToken cancellationToken = default);
}
