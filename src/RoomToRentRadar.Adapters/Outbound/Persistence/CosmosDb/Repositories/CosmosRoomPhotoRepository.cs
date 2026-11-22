using System.Collections.Concurrent;
using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosRoomPhotoRepository : IRoomPhotoRepository
{
    private const int MaxConcurrencyRetries = 10;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RoomLocks = new(StringComparer.OrdinalIgnoreCase);

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
                p => !p.IsDeleted &&
                     !string.Equals(p.DocumentType, RoomPhotoDocument.GalleryStateDocumentType, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(p.RoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

            return matched
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.UploadedAtUtc)
                .Select(p => p.ToDomain())
                .ToList();
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.roomId = @roomId AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) AND (NOT IS_DEFINED(c.documentType) OR c.documentType = @docType) ORDER BY c.displayOrder ASC")
            .WithParameter("@roomId", normalizedRoomId)
            .WithParameter("@docType", RoomPhotoDocument.PhotoDocumentType);

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

        if (doc is null ||
            doc.IsDeleted ||
            string.Equals(doc.DocumentType, RoomPhotoDocument.GalleryStateDocumentType, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return doc.ToDomain();
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

    public async Task<RoomPhoto> AddWithinQuotaAsync(
        string roomId,
        int maxPhotosPerRoom,
        bool requestCover,
        Func<int, bool, RoomPhoto> photoFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(photoFactory);

        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var normalizedRoomId = roomId.Trim();
        var stateId = BuildGalleryStateDocumentId(normalizedRoomId);
        var roomLock = RoomLocks.GetOrAdd(normalizedRoomId, _ => new SemaphoreSlim(1, 1));

        await roomLock.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                var stateDoc = await _dbContext.RoomPhotos.ReadItemAsync(stateId, normalizedRoomId, cancellationToken);
                var existingPhotos = await GetByRoomIdAsync(normalizedRoomId, cancellationToken);

                var effectiveCount = Math.Max(existingPhotos.Count, stateDoc?.PhotoCount ?? 0);
                if (effectiveCount >= maxPhotosPerRoom)
                {
                    throw new RoomPhotoQuotaExceededException(
                        $"Room '{normalizedRoomId}' has reached the maximum limit of {maxPhotosPerRoom} photos.");
                }

                var isCover = requestCover || effectiveCount == 0;
                var maxExistingOrder = existingPhotos.Count == 0 ? -1 : existingPhotos.Max(p => p.DisplayOrder);
                var nextDisplayOrder = Math.Max(maxExistingOrder + 1, stateDoc?.NextDisplayOrder ?? 0);

                var newPhoto = photoFactory(nextDisplayOrder, isCover);
                var updatedState = new RoomPhotoDocument
                {
                    Id = stateId,
                    RoomId = normalizedRoomId,
                    DocumentType = RoomPhotoDocument.GalleryStateDocumentType,
                    PhotoCount = effectiveCount + 1,
                    NextDisplayOrder = nextDisplayOrder + 1,
                    CoverPhotoId = isCover
                        ? newPhoto.Id
                        : (existingPhotos.FirstOrDefault(p => p.IsCover)?.Id ?? stateDoc?.CoverPhotoId),
                    StateVersion = (stateDoc?.StateVersion ?? 0) + 1
                };

                try
                {
                    if (stateDoc is null)
                    {
                        await _dbContext.RoomPhotos.CreateItemAsync(updatedState, normalizedRoomId, cancellationToken);
                    }
                    else
                    {
                        await _dbContext.RoomPhotos.ReplaceItemAsync(
                            updatedState,
                            stateId,
                            normalizedRoomId,
                            ifMatchEtag: stateDoc.ETag,
                            cancellationToken: cancellationToken);
                    }
                }
                catch (CosmosException ex) when (
                    (ex.StatusCode == HttpStatusCode.PreconditionFailed || ex.StatusCode == HttpStatusCode.Conflict) &&
                    attempt < MaxConcurrencyRetries)
                {
                    continue;
                }

                var addedNewPhoto = false;
                var demotedCovers = new List<RoomPhoto>();

                try
                {
                    await _dbContext.RoomPhotos.CreateItemAsync(
                        RoomPhotoDocument.FromDomain(newPhoto),
                        normalizedRoomId,
                        cancellationToken);
                    addedNewPhoto = true;

                    if (isCover)
                    {
                        foreach (var previousCover in existingPhotos.Where(p => p.IsCover && !string.Equals(p.Id, newPhoto.Id, StringComparison.Ordinal)))
                        {
                            previousCover.SetAsCover(false);
                            try
                            {
                                await _dbContext.RoomPhotos.UpsertItemAsync(
                                    RoomPhotoDocument.FromDomain(previousCover),
                                    normalizedRoomId,
                                    cancellationToken);
                                demotedCovers.Add(previousCover);
                            }
                            catch
                            {
                                previousCover.SetAsCover(true);
                                throw;
                            }
                        }
                    }

                    return newPhoto;
                }
                catch
                {
                    foreach (var demotedCover in demotedCovers)
                    {
                        try
                        {
                            demotedCover.SetAsCover(true);
                            await _dbContext.RoomPhotos.UpsertItemAsync(
                                RoomPhotoDocument.FromDomain(demotedCover),
                                normalizedRoomId,
                                CancellationToken.None);
                        }
                        catch
                        {
                            // Best-effort rollback; preserve original persistence exception.
                        }
                    }

                    if (addedNewPhoto)
                    {
                        try
                        {
                            await _dbContext.RoomPhotos.DeleteItemAsync(newPhoto.Id, normalizedRoomId, CancellationToken.None);
                        }
                        catch
                        {
                            // Best-effort rollback; preserve original persistence exception.
                        }
                    }

                    await TrySyncGalleryStateAsync(normalizedRoomId, CancellationToken.None);
                    throw;
                }
            }

            throw new RoomPhotoQuotaExceededException(
                $"Room '{normalizedRoomId}' could not complete the upload due to concurrent gallery updates.");
        }
        finally
        {
            roomLock.Release();
        }
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
        var normalizedRoomId = roomId.Trim();
        var roomLock = RoomLocks.GetOrAdd(normalizedRoomId, _ => new SemaphoreSlim(1, 1));

        await roomLock.WaitAsync(cancellationToken);
        try
        {
            await _dbContext.RoomPhotos.DeleteItemAsync(photoId.Trim(), normalizedRoomId, cancellationToken);
            await TrySyncGalleryStateAsync(normalizedRoomId, cancellationToken);
        }
        finally
        {
            roomLock.Release();
        }
    }

    private async Task TrySyncGalleryStateAsync(string normalizedRoomId, CancellationToken cancellationToken)
    {
        var stateId = BuildGalleryStateDocumentId(normalizedRoomId);
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                var stateDoc = await _dbContext.RoomPhotos.ReadItemAsync(stateId, normalizedRoomId, cancellationToken);
                if (stateDoc is null)
                {
                    return;
                }

                var remaining = await GetByRoomIdAsync(normalizedRoomId, cancellationToken);
                var updatedState = new RoomPhotoDocument
                {
                    Id = stateId,
                    RoomId = normalizedRoomId,
                    DocumentType = RoomPhotoDocument.GalleryStateDocumentType,
                    PhotoCount = remaining.Count,
                    NextDisplayOrder = remaining.Count == 0
                        ? stateDoc.NextDisplayOrder
                        : Math.Max(remaining.Max(p => p.DisplayOrder) + 1, stateDoc.NextDisplayOrder),
                    CoverPhotoId = remaining.FirstOrDefault(p => p.IsCover)?.Id,
                    StateVersion = stateDoc.StateVersion + 1
                };

                await _dbContext.RoomPhotos.ReplaceItemAsync(
                    updatedState,
                    stateId,
                    normalizedRoomId,
                    ifMatchEtag: stateDoc.ETag,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed && attempt < MaxConcurrencyRetries)
            {
                continue;
            }
            catch
            {
                return;
            }
        }
    }

    private static string BuildGalleryStateDocumentId(string normalizedRoomId) =>
        $"__gallery_state__:{normalizedRoomId}";
}
