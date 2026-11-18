using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Favourites;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Student favourites (Favourites tag of <c>docs/api/openapi.yaml</c>, US06, BR-FAV-02).</summary>
public interface IFavouriteGateway
{
    [ApiOperation("listMyFavourites")]
    Task<PagedResult<FavouriteDto>> ListMyFavouritesAsync(string? campusId = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("addFavourite")]
    Task AddFavouriteAsync(string roomId, CancellationToken ct = default);

    [ApiOperation("removeFavourite")]
    Task RemoveFavouriteAsync(string roomId, CancellationToken ct = default);
}