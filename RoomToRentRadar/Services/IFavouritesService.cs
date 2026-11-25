using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

public interface IFavouritesService
{
    event EventHandler? FavouritesChanged;

    Task<IReadOnlyList<RoomListingItemModel>> GetFavouritesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsFavouriteAsync(string listingId, CancellationToken cancellationToken = default);

    Task<bool> ToggleFavouriteAsync(RoomListingItemModel room, CancellationToken cancellationToken = default);

    Task AddFavouriteAsync(RoomListingItemModel room, CancellationToken cancellationToken = default);

    Task RemoveFavouriteAsync(string listingId, CancellationToken cancellationToken = default);
}