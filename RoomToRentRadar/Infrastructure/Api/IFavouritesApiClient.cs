using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Infrastructure.Api;

public enum FavouritesFetchOutcome
{
    Found,
    Unauthorized
}

public sealed record FavouritesFetchResult(
    FavouritesFetchOutcome Outcome,
    IReadOnlyList<FavouriteRoomDto> Items);

public interface IFavouritesApiClient
{
    Task<FavouritesFetchResult> GetFavouritesAsync(CancellationToken cancellationToken = default);

    Task AddFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task RemoveFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default);
}
