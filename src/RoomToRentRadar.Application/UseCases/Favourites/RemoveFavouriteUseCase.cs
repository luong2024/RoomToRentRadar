using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Favourites;

public sealed class RemoveFavouriteUseCase : IRemoveFavouriteUseCase
{
    private readonly IFavouriteRepository _favouriteRepository;

    public RemoveFavouriteUseCase(IFavouriteRepository favouriteRepository)
    {
        _favouriteRepository = favouriteRepository ?? throw new ArgumentNullException(nameof(favouriteRepository));
    }

    public async Task ExecuteAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new RequestValidationException("userId", "User identifier cannot be empty.");
        }

        if (!FavouriteRoom.TryValidateRoomId(roomId, out var trimmedRoomId, out var errorMessage))
        {
            throw new RequestValidationException("roomId", errorMessage!);
        }

        await _favouriteRepository.RemoveAsync(userId, trimmedRoomId, cancellationToken);
    }
}
