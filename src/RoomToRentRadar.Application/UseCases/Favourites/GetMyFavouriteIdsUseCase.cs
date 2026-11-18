using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases.Favourites;

public sealed class GetMyFavouriteIdsUseCase : IGetMyFavouriteIdsUseCase
{
    private readonly IFavouriteRepository _favouriteRepository;

    public GetMyFavouriteIdsUseCase(IFavouriteRepository favouriteRepository)
    {
        _favouriteRepository = favouriteRepository ?? throw new ArgumentNullException(nameof(favouriteRepository));
    }

    public async Task<FavouriteIdsResultDto> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new RequestValidationException("userId", "User identifier cannot be empty.");
        }

        var favourites = await _favouriteRepository.GetByUserIdAsync(userId, cancellationToken);

        var roomIds = favourites
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenBy(f => f.RoomId, StringComparer.Ordinal)
            .Select(f => f.RoomId)
            .ToList();

        return new FavouriteIdsResultDto(roomIds, roomIds.Count);
    }
}
