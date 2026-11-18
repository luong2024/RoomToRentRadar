using System.Collections.Concurrent;
using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosFavouriteRepository : IFavouriteRepository
{
    private const int MaxOptimisticConcurrencyRetries = 30;
    private const string QuotaGuardRoomIdSentinel = "__quota_guard__";

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _userWriteLocks = new();
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

        return await AddIfNotExistsCoreAsync(favourite, cancellationToken);
    }

    public async Task<(FavouriteRoom? Favourite, FavouriteAddStatus Status)> TryAddWithQuotaAsync(
        FavouriteRoom favourite,
        int maxQuota,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(favourite);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxQuota, 1);

        await _dbContext.EnsureFavouritesContainerAsync(cancellationToken);

        var userLock = _userWriteLocks.GetOrAdd(favourite.UserId, static _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(cancellationToken);

        try
        {
            var partitionKey = favourite.UserId.ToString("D");

            for (var attempt = 0; attempt < MaxOptimisticConcurrencyRetries; attempt++)
            {
                // 1. Point-read check for idempotency
                var existingDoc = await _dbContext.Favourites.ReadItemAsync(
                    favourite.Id,
                    partitionKey,
                    cancellationToken);

                if (existingDoc is not null)
                {
                    return (existingDoc.ToDomain(), FavouriteAddStatus.AlreadyExists);
                }

                // 2. Load per-user distributed quota guard document (with Cosmos _etag)
                var quotaDoc = await GetOrCreateQuotaGuardDocumentAsync(favourite.UserId, cancellationToken);

                // 3. Check current count in the user's partition before inserting
                var currentItems = await GetByUserIdAsync(favourite.UserId, cancellationToken);
                if (currentItems.Count >= maxQuota)
                {
                    return (null, FavouriteAddStatus.QuotaExceeded);
                }

                // 4. Advance quota guard _etag with If-Match optimistic concurrency control
                //    so concurrent API instances cannot both pass the quota check simultaneously
                quotaDoc.CreatedAtUtc = DateTimeOffset.UtcNow;
                try
                {
                    await _dbContext.Favourites.ReplaceItemAsync(
                        quotaDoc,
                        quotaDoc.Id,
                        quotaDoc.UserId,
                        ifMatchEtag: quotaDoc.ETag,
                        cancellationToken: cancellationToken);
                }
                catch (CosmosException ex) when (
                    ex.StatusCode == HttpStatusCode.PreconditionFailed ||
                    ex.StatusCode == HttpStatusCode.NotFound ||
                    ex.StatusCode == HttpStatusCode.Conflict)
                {
                    continue;
                }

                // 5. Insert the favourite item
                var (saved, wasCreated) = await AddIfNotExistsCoreAsync(favourite, cancellationToken);
                if (!wasCreated)
                {
                    return (saved, FavouriteAddStatus.AlreadyExists);
                }

                // 6. Post-insert verification & rollback guard against overlapping distributed writes
                var postInsertItems = await GetByUserIdAsync(favourite.UserId, cancellationToken);
                if (postInsertItems.Count > maxQuota)
                {
                    var allowedIds = postInsertItems
                        .OrderBy(f => f.CreatedAtUtc)
                        .ThenBy(f => f.Id, StringComparer.Ordinal)
                        .Take(maxQuota)
                        .Select(f => f.Id)
                        .ToHashSet(StringComparer.Ordinal);

                    if (!allowedIds.Contains(saved.Id))
                    {
                        await _dbContext.Favourites.DeleteItemAsync(saved.Id, partitionKey, CancellationToken.None);
                        return (null, FavouriteAddStatus.QuotaExceeded);
                    }
                }

                return (saved, FavouriteAddStatus.Created);
            }

            return (null, FavouriteAddStatus.QuotaExceeded);
        }
        finally
        {
            userLock.Release();
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

    private async Task<FavouriteDocument> GetOrCreateQuotaGuardDocumentAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var guardId = $"quota:{userId:D}";

        var existing = await _dbContext.Favourites.ReadItemAsync(
            guardId,
            guardId,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var guardDoc = new FavouriteDocument
        {
            Id = guardId,
            UserId = guardId,
            RoomId = QuotaGuardRoomIdSentinel,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            return await _dbContext.Favourites.CreateItemAsync(
                guardDoc,
                guardId,
                cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            var createdByPeer = await _dbContext.Favourites.ReadItemAsync(
                guardId,
                guardId,
                cancellationToken);

            return createdByPeer ?? guardDoc;
        }
    }

    private async Task<(FavouriteRoom Favourite, bool WasCreated)> AddIfNotExistsCoreAsync(
        FavouriteRoom favourite,
        CancellationToken cancellationToken)
    {
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
            var existing = await _dbContext.Favourites.ReadItemAsync(
                doc.Id,
                partitionKey,
                cancellationToken);

            if (existing is not null)
            {
                return (existing.ToDomain(), false);
            }

            var upserted = await _dbContext.Favourites.UpsertItemAsync(
                doc,
                partitionKey,
                cancellationToken);

            return (upserted.ToDomain(), true);
        }
    }
}
