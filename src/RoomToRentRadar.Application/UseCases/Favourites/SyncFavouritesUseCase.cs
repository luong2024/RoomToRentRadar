using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Favourites;

public sealed class SyncFavouritesUseCase : ISyncFavouritesUseCase
{
    public const int MaxSyncBatchSize = 100;
    public const int MaxFavouritesPerUser = AddFavouriteUseCase.MaxFavouritesPerUser;

    private readonly IFavouriteRepository _favouriteRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly TimeProvider _timeProvider;

    public SyncFavouritesUseCase(
        IFavouriteRepository favouriteRepository,
        IRoomRepository roomRepository,
        TimeProvider? timeProvider = null)
    {
        _favouriteRepository = favouriteRepository ?? throw new ArgumentNullException(nameof(favouriteRepository));
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SyncFavouritesResultDto> ExecuteAsync(
        Guid userId,
        IReadOnlyCollection<string?>? roomIds,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new RequestValidationException("userId", "User identifier cannot be empty.");
        }

        if (roomIds is not null && roomIds.Count > MaxSyncBatchSize)
        {
            throw new RequestValidationException(
                "roomIds",
                $"Cannot synchronize more than {MaxSyncBatchSize} room IDs in a single request.");
        }

        var skippedRoomIds = new List<string>();
        var skippedSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validCandidates = new List<string>();
        var candidateSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (roomIds is not null)
        {
            foreach (var rawId in roomIds)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    continue;
                }

                var trimmed = rawId.Trim();
                if (!FavouriteRoom.TryValidateRoomId(trimmed, out var validTrimmed, out _))
                {
                    if (skippedSeen.Add(trimmed))
                    {
                        skippedRoomIds.Add(trimmed);
                    }
                    continue;
                }

                if (candidateSeen.Add(validTrimmed))
                {
                    validCandidates.Add(validTrimmed);
                }
            }
        }

        var existingFavourites = await _favouriteRepository.GetByUserIdAsync(userId, cancellationToken);
        var existingByNormalizedId = existingFavourites
            .GroupBy(f => f.NormalizedRoomId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var newCandidateIds = validCandidates
            .Where(id => !existingByNormalizedId.ContainsKey(id))
            .ToList();

        var addedCount = 0;
        var allFavourites = new List<FavouriteRoom>(existingFavourites);

        if (newCandidateIds.Count > 0)
        {
            var candidateRooms = await _roomRepository.GetByIdsAsync(newCandidateIds, cancellationToken);
            var candidateRoomsById = candidateRooms
                .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var currentCount = allFavourites.Count;

            foreach (var candidateId in newCandidateIds)
            {
                if (!candidateRoomsById.TryGetValue(candidateId, out var room) || !room.IsVerified)
                {
                    if (skippedSeen.Add(candidateId))
                    {
                        skippedRoomIds.Add(candidateId);
                    }
                    continue;
                }

                if (currentCount >= MaxFavouritesPerUser)
                {
                    if (skippedSeen.Add(candidateId))
                    {
                        skippedRoomIds.Add(candidateId);
                    }
                    continue;
                }

                var now = _timeProvider.GetUtcNow();
                var favourite = FavouriteRoom.Create(userId, room.Id, now);
                var (saved, wasCreated) = await _favouriteRepository.AddIfNotExistsAsync(favourite, cancellationToken);

                if (wasCreated)
                {
                    addedCount++;
                    currentCount++;
                    allFavourites.Add(saved);
                    existingByNormalizedId[saved.NormalizedRoomId] = saved;
                }
                else if (!existingByNormalizedId.ContainsKey(saved.NormalizedRoomId))
                {
                    allFavourites.Add(saved);
                    existingByNormalizedId[saved.NormalizedRoomId] = saved;
                }
            }
        }

        if (allFavourites.Count == 0)
        {
            return new SyncFavouritesResultDto(
                AddedCount: addedCount,
                SkippedRoomIds: skippedRoomIds,
                Favourites: [],
                TotalCount: 0);
        }

        var orderedFavourites = allFavourites
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenBy(f => f.RoomId, StringComparer.Ordinal)
            .ToList();

        var allFavouriteRoomIds = orderedFavourites
            .Select(f => f.RoomId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allRooms = await _roomRepository.GetByIdsAsync(allFavouriteRoomIds, cancellationToken);
        var allRoomsById = allRooms
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var favouriteDtos = orderedFavourites
            .Select(fav =>
            {
                allRoomsById.TryGetValue(fav.RoomId, out var room);
                return FavouriteRoomMapper.ToDto(fav, room);
            })
            .ToList();

        return new SyncFavouritesResultDto(
            AddedCount: addedCount,
            SkippedRoomIds: skippedRoomIds,
            Favourites: favouriteDtos,
            TotalCount: favouriteDtos.Count);
    }
}
