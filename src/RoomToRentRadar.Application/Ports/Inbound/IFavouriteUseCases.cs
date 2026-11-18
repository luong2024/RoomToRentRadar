using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IGetMyFavouritesUseCase
{
    Task<FavouriteListResultDto> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IGetMyFavouriteIdsUseCase
{
    Task<FavouriteIdsResultDto> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IAddFavouriteUseCase
{
    Task<FavouriteRoomItemDto> ExecuteAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default);
}

public interface IRemoveFavouriteUseCase
{
    Task ExecuteAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default);
}

public interface ISyncFavouritesUseCase
{
    Task<SyncFavouritesResultDto> ExecuteAsync(
        Guid userId,
        IReadOnlyCollection<string?>? roomIds,
        CancellationToken cancellationToken = default);
}
