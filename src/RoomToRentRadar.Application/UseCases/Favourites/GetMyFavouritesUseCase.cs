using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases.Favourites;

public sealed class GetMyFavouritesUseCase : IGetMyFavouritesUseCase
{
    private readonly IFavouriteRepository _favouriteRepository;
    private readonly IRoomRepository _roomRepository;

    public GetMyFavouritesUseCase(
        IFavouriteRepository favouriteRepository,
        IRoomRepository roomRepository)
    {
        _favouriteRepository = favouriteRepository ?? throw new ArgumentNullException(nameof(favouriteRepository));
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
    }

    public async Task<FavouriteListResultDto> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new RequestValidationException("userId", "User identifier cannot be empty.");
        }

        var favourites = await _favouriteRepository.GetByUserIdAsync(userId, cancellationToken);
        if (favourites.Count == 0)
        {
            return new FavouriteListResultDto([], 0);
        }

        var orderedFavourites = favourites
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenBy(f => f.RoomId, StringComparer.Ordinal)
            .ToList();

        var roomIds = orderedFavourites
            .Select(f => f.RoomId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rooms = await _roomRepository.GetByIdsAsync(roomIds, cancellationToken);
        var roomsById = rooms
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var items = orderedFavourites
            .Select(fav =>
            {
                roomsById.TryGetValue(fav.RoomId, out var room);
                return FavouriteRoomMapper.ToDto(fav, room);
            })
            .ToList();

        return new FavouriteListResultDto(items, items.Count);
    }
}
