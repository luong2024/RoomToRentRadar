using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosFavouriteRepository : IFavouriteRepository
{
    private readonly ICosmosDbContext _dbContext;

    public CosmosFavouriteRepository(ICosmosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<FavouriteRoom?> GetByUserAndRoomIdAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !FavouriteRoom.TryValidateRoomId(roomId, out var trimmedRoomId, out _))
        {
            return null;
        }

        await _dbContext.EnsureFavouritesContainerAsync(cancellationToken);

        var id = FavouriteRoom.BuildId(userId, trimmedRoomId);
        var partitionKey = userId.ToString("D");

        var doc = await _dbContext.Favourites.ReadItemAsync(id, partitionKey, cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<FavouriteRoom>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<FavouriteRoom>();
        }

        await _dbContext.EnsureFavouritesContainerAsync(cancellationToken);

        var partitionKey = userId.ToString("D");

        if (_dbContext.Favourites is InMemoryCosmosContainer<FavouriteDocument> inMemory)
        {
            var matched = await inMemory.QueryAsync(
                f => string.Equals(f.UserId, partitionKey, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

            return matched
                .OrderByDescending(f => f.CreatedAtUtc)
                .ThenBy(f => f.RoomId, StringComparer.Ordinal)
                .Select(f => f.ToDomain())
                .ToList();
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.createdAtUtc DESC, c.roomId ASC")
            .WithParameter("@userId", partitionKey);

        var docs = await _dbContext.Favourites.QueryAsync(query, cancellationToken);
        return docs
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenBy(f => f.RoomId, StringComparer.Ordinal)
            .Select(f => f.ToDomain())
            .ToList();
    }

    public async Task<int> CountByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetByUserIdAsync(userId, cancellationToken);
        return items.Count;
    }

    public async Task<(FavouriteRoom Favourite, bool WasCreated)> AddIfNotExistsAsync(
        FavouriteRoom favourite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(favourite);

        await _dbContext.EnsureFavouritesContainerAsync(cancellationToken);

        var doc = FavouriteDocument.FromDomain(favourite);
        doc.ETag = null;
        var partitionKey = doc.UserId;

        try
        {
            var created = await _dbContext.Favourites.CreateItemAsync(
                doc,
                partitionKey,
                cancellationToken);

            return (created.ToDomain(), true);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Concurrent insert or already existing item: read and return idempotently
            var existing = await _dbContext.Favourites.ReadItemAsync(
                doc.Id,
                partitionKey,
                cancellationToken);

            if (existing is not null)
            {
                return (existing.ToDomain(), false);
            }

            // Fallback if concurrently deleted right after conflict
            var upserted = await _dbContext.Favourites.UpsertItemAsync(
                doc,
                partitionKey,
                cancellationToken);

            return (upserted.ToDomain(), true);
        }
    }

    public async Task RemoveAsync(
        Guid userId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !FavouriteRoom.TryValidateRoomId(roomId, out var trimmedRoomId, out _))
        {
            return;
        }

        await _dbContext.EnsureFavouritesContainerAsync(cancellationToken);

        var id = FavouriteRoom.BuildId(userId, trimmedRoomId);
        var partitionKey = userId.ToString("D");

        await _dbContext.Favourites.DeleteItemAsync(id, partitionKey, cancellationToken);
    }
}
