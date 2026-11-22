using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosRoomPhotoRepository : IRoomPhotoRepository
{
    private readonly ICosmosDbContext _dbContext;

    public CosmosRoomPhotoRepository(ICosmosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<RoomPhoto>> GetByRoomIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Array.Empty<RoomPhoto>();
        }

        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var normalizedRoomId = roomId.Trim();

        if (_dbContext.RoomPhotos is InMemoryCosmosContainer<RoomPhotoDocument> inMemory)
        {
            var matched = await inMemory.QueryAsync(
                p => !p.IsDeleted && string.Equals(p.RoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

            return matched
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.UploadedAtUtc)
                .Select(p => p.ToDomain())
                .ToList();
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.roomId = @roomId AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) ORDER BY c.displayOrder ASC")
            .WithParameter("@roomId", normalizedRoomId);

        var docs = await _dbContext.RoomPhotos.QueryAsync(query, cancellationToken);
        return docs
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.UploadedAtUtc)
            .Select(p => p.ToDomain())
            .ToList();
    }

    public async Task<RoomPhoto?> GetByIdAsync(
        string roomId,
        string photoId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(photoId))
        {
            return null;
        }

        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var doc = await _dbContext.RoomPhotos.ReadItemAsync(
            photoId.Trim(),
            roomId.Trim(),
            cancellationToken);

        return doc is null || doc.IsDeleted ? null : doc.ToDomain();
    }

    public async Task<int> CountByRoomIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetByRoomIdAsync(roomId, cancellationToken);
        return items.Count;
    }

    public async Task AddAsync(RoomPhoto photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var doc = RoomPhotoDocument.FromDomain(photo);
        await _dbContext.RoomPhotos.CreateItemAsync(doc, doc.RoomId, cancellationToken);
    }

    public async Task UpdateAsync(RoomPhoto photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var doc = RoomPhotoDocument.FromDomain(photo);
        await _dbContext.RoomPhotos.UpsertItemAsync(doc, doc.RoomId, cancellationToken);
    }

    public async Task DeleteAsync(
        string roomId,
        string photoId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(photoId))
        {
            return;
        }

        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);
        await _dbContext.RoomPhotos.DeleteItemAsync(photoId.Trim(), roomId.Trim(), cancellationToken);
    }
}
