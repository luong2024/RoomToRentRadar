using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Favourites;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>Favourites endpoints arrive with T06.1; every operation is a task-id stub until then (PLAN §5.4).</summary>
public sealed class HttpFavouriteGateway : IFavouriteGateway
{
    public HttpFavouriteGateway(HttpClient httpClient, ILogger<HttpFavouriteGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<PagedResult<FavouriteDto>> ListMyFavouritesAsync(string? campusId = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedResult<FavouriteDto>>("T06.1", "listMyFavourites");

    public Task AddFavouriteAsync(string roomId, CancellationToken ct = default) =>
        Pending.For("T06.1", "addFavourite");

    public Task RemoveFavouriteAsync(string roomId, CancellationToken ct = default) =>
        Pending.For("T06.1", "removeFavourite");
}