using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Favourites;

public sealed class AddFavouriteUseCase : IAddFavouriteUseCase
{
    public const int MaxFavouritesPerUser = 200;

    private readonly IFavouriteRepository _favouriteRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly TimeProvider _timeProvider;

    public AddFavouriteUseCase(
        IFavouriteRepository favouriteRepository,
        IRoomRepository roomRepository,
        TimeProvider? timeProvider = null)
    {
        _favouriteRepository = favouriteRepository ?? throw new ArgumentNullException(nameof(favouriteRepository));
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<FavouriteRoomItemDto> ExecuteAsync(
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

        // 1. Check idempotency first: if the room is already in the user's favourites, return it directly
        var existing = await _favouriteRepository.GetByUserAndRoomIdAsync(userId, trimmedRoomId, cancellationToken);
        if (existing is not null)
        {
            var existingRoom = await _roomRepository.GetByIdAsync(existing.RoomId, cancellationToken);
            return FavouriteRoomMapper.ToDto(existing, existingRoom);
        }

        // 2. Verify room existence and verification state
        var room = await _roomRepository.GetByIdAsync(trimmedRoomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Room with ID '{trimmedRoomId}' was not found.");
        }

        if (!room.IsVerified)
        {
            throw new RequestValidationException(
                "roomId",
                $"Room '{room.Id}' is not verified and cannot be added to favourites.");
        }

        // 3. Enforce per-account favourite quota
        var currentCount = await _favouriteRepository.CountByUserIdAsync(userId, cancellationToken);
        if (currentCount >= MaxFavouritesPerUser)
        {
            throw new RequestValidationException(
                "favourites",
                $"Favourites limit of {MaxFavouritesPerUser} rooms per account has been reached.");
        }

        // 4. Persist using the canonical Room.Id so Cosmos DB case-sensitive queries match accurately
        var now = _timeProvider.GetUtcNow();
        var favourite = FavouriteRoom.Create(userId, room.Id, now);
        var (saved, _) = await _favouriteRepository.AddIfNotExistsAsync(favourite, cancellationToken);

        return FavouriteRoomMapper.ToDto(saved, room);
    }
}
