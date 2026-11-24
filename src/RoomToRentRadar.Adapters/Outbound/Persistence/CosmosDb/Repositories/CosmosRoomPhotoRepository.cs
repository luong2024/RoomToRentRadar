using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosRoomPhotoRepository : IRoomPhotoRepository
{
    private const int MaxConcurrencyRetries = 12;

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
        var docs = await GetActivePhotoDocumentsAsync(roomId.Trim(), cancellationToken);
        return docs.Select(d => d.ToDomain()).ToList();
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

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var stateDoc = await _dbContext.RoomPhotos.ReadItemAsync(stateId, normalizedRoomId, cancellationToken);
            var existingDocs = await GetActivePhotoDocumentsAsync(normalizedRoomId, cancellationToken);

            if (existingDocs.Count >= maxPhotosPerRoom)
            {
                throw new RoomPhotoQuotaExceededException(
                    $"Room '{normalizedRoomId}' has reached the maximum limit of {maxPhotosPerRoom} photos.");
            }

            var isCover = requestCover || existingDocs.Count == 0;
            var maxExistingOrder = existingDocs.Count == 0 ? -1 : existingDocs.Max(d => d.DisplayOrder);
            var nextDisplayOrder = Math.Max(maxExistingOrder + 1, stateDoc?.NextDisplayOrder ?? 0);

            var newPhoto = photoFactory(nextDisplayOrder, isCover);
            var newPhotoDoc = RoomPhotoDocument.FromDomain(newPhoto);

            var updatedState = new RoomPhotoDocument
            {
                Id = stateId,
                RoomId = normalizedRoomId,
                DocumentType = RoomPhotoDocument.GalleryStateDocumentType,
                PhotoCount = existingDocs.Count + 1,
                NextDisplayOrder = nextDisplayOrder + 1,
                CoverPhotoId = isCover
                    ? newPhoto.Id
                    : existingDocs.FirstOrDefault(d => d.IsCover)?.Id,
                StateVersion = (stateDoc?.StateVersion ?? 0) + 1
            };

            var batch = new List<CosmosBatchOperation<RoomPhotoDocument>>
            {
                stateDoc is null
                    ? CosmosBatchOperation<RoomPhotoDocument>.Create(stateId, updatedState)
                    : CosmosBatchOperation<RoomPhotoDocument>.Replace(stateId, updatedState, stateDoc.ETag),
                CosmosBatchOperation<RoomPhotoDocument>.Create(newPhotoDoc.Id, newPhotoDoc)
            };

            if (isCover)
            {
                foreach (var previousCover in existingDocs.Where(d => d.IsCover))
                {
                    var originalEtag = previousCover.ETag;
                    previousCover.IsCover = false;
                    batch.Add(CosmosBatchOperation<RoomPhotoDocument>.Replace(previousCover.Id, previousCover, originalEtag));
                }
            }

            try
            {
                await _dbContext.RoomPhotos.ExecuteTransactionalBatchAsync(normalizedRoomId, batch, cancellationToken);
                return newPhoto;
            }
            catch (CosmosException ex) when (IsRetryableConcurrencyStatus(ex.StatusCode))
            {
                if (attempt < MaxConcurrencyRetries)
                {
                    continue;
                }

                throw new ConcurrencyConflictException(
                    $"Room '{normalizedRoomId}' could not complete the upload due to concurrent gallery updates. Please retry.");
            }
        }

        throw new ConcurrencyConflictException(
            $"Room '{normalizedRoomId}' could not complete the upload due to concurrent gallery updates. Please retry.");
    }

    public async Task<IReadOnlyList<RoomPhoto>> ReorderAsync(
        string roomId,
        IReadOnlyList<string> orderedPhotoIds,
        string? coverPhotoId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var normalizedRoomId = roomId.Trim();
        var stateId = BuildGalleryStateDocumentId(normalizedRoomId);
        var normalizedOrderedIds = (orderedPhotoIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        if (normalizedOrderedIds.Distinct(StringComparer.Ordinal).Count() != normalizedOrderedIds.Count)
        {
            throw new InvalidRoomPhotoException("Ordered photo IDs cannot contain duplicates.");
        }

        var normalizedCoverId = string.IsNullOrWhiteSpace(coverPhotoId) ? null : coverPhotoId.Trim();

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var stateDoc = await _dbContext.RoomPhotos.ReadItemAsync(stateId, normalizedRoomId, cancellationToken);
            var existingDocs = await GetActivePhotoDocumentsAsync(normalizedRoomId, cancellationToken);
            var docsById = existingDocs.ToDictionary(d => d.Id, StringComparer.Ordinal);

            foreach (var photoId in normalizedOrderedIds)
            {
                if (!docsById.ContainsKey(photoId))
                {
                    throw new RoomPhotoNotFoundException($"Photo '{photoId}' was not found in room '{normalizedRoomId}'.");
                }
            }

            if (normalizedCoverId is not null && !docsById.ContainsKey(normalizedCoverId))
            {
                throw new RoomPhotoNotFoundException($"Cover photo '{normalizedCoverId}' was not found in room '{normalizedRoomId}'.");
            }

            if (existingDocs.Count == 0)
            {
                return Array.Empty<RoomPhoto>();
            }

            if (normalizedOrderedIds.Count > 0)
            {
                var nextOrder = 0;
                foreach (var photoId in normalizedOrderedIds)
                {
                    docsById[photoId].DisplayOrder = nextOrder++;
                }

                foreach (var remaining in existingDocs
                             .Where(d => !normalizedOrderedIds.Contains(d.Id, StringComparer.Ordinal))
                             .OrderBy(d => d.DisplayOrder)
                             .ThenBy(d => d.UploadedAtUtc))
                {
                    remaining.DisplayOrder = nextOrder++;
                }
            }

            if (normalizedCoverId is not null)
            {
                foreach (var doc in existingDocs)
                {
                    doc.IsCover = string.Equals(doc.Id, normalizedCoverId, StringComparison.Ordinal);
                }
            }

            var updatedState = new RoomPhotoDocument
            {
                Id = stateId,
                RoomId = normalizedRoomId,
                DocumentType = RoomPhotoDocument.GalleryStateDocumentType,
                PhotoCount = existingDocs.Count,
                NextDisplayOrder = Math.Max(existingDocs.Max(d => d.DisplayOrder) + 1, stateDoc?.NextDisplayOrder ?? 0),
                CoverPhotoId = existingDocs.FirstOrDefault(d => d.IsCover)?.Id,
                StateVersion = (stateDoc?.StateVersion ?? 0) + 1
            };

            var batch = new List<CosmosBatchOperation<RoomPhotoDocument>>
            {
                stateDoc is null
                    ? CosmosBatchOperation<RoomPhotoDocument>.Create(stateId, updatedState)
                    : CosmosBatchOperation<RoomPhotoDocument>.Replace(stateId, updatedState, stateDoc.ETag)
            };

            // Use conditional Replace (never Upsert) with ETag so concurrently deleted photos cannot be resurrected.
            foreach (var doc in existingDocs)
            {
                batch.Add(CosmosBatchOperation<RoomPhotoDocument>.Replace(doc.Id, doc, doc.ETag));
            }

            try
            {
                await _dbContext.RoomPhotos.ExecuteTransactionalBatchAsync(normalizedRoomId, batch, cancellationToken);
                return existingDocs
                    .OrderBy(d => d.DisplayOrder)
                    .ThenBy(d => d.UploadedAtUtc)
                    .Select(d => d.ToDomain())
                    .ToList();
            }
            catch (CosmosException ex) when (IsRetryableConcurrencyStatus(ex.StatusCode))
            {
                if (attempt < MaxConcurrencyRetries)
                {
                    continue;
                }

                throw new ConcurrencyConflictException(
                    $"Room '{normalizedRoomId}' gallery could not be reordered due to concurrent updates. Please retry.");
            }
        }

        throw new ConcurrencyConflictException(
            $"Room '{normalizedRoomId}' gallery could not be reordered due to concurrent updates. Please retry.");
    }

    public async Task<RoomPhoto?> MarkPendingDeletionAsync(
        string roomId,
        string photoId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(photoId))
        {
            return null;
        }

        await _dbContext.EnsureRoomPhotosContainerAsync(cancellationToken);

        var normalizedRoomId = roomId.Trim();
        var normalizedPhotoId = photoId.Trim();
        var stateId = BuildGalleryStateDocumentId(normalizedRoomId);

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var stateDoc = await _dbContext.RoomPhotos.ReadItemAsync(stateId, normalizedRoomId, cancellationToken);
            var existingDocs = await GetActivePhotoDocumentsAsync(normalizedRoomId, cancellationToken);
            var targetDoc = existingDocs.FirstOrDefault(d => string.Equals(d.Id, normalizedPhotoId, StringComparison.Ordinal));
            if (targetDoc is null)
            {
                // If the photo is already marked pending deletion (IsDeleted == true), return it so
                // retrying DELETE can complete R2 object cleanup and hard-delete the tombstone.
                var rawDoc = await _dbContext.RoomPhotos.ReadItemAsync(normalizedPhotoId, normalizedRoomId, cancellationToken);
                if (rawDoc is not null &&
                    rawDoc.IsDeleted &&
                    !string.Equals(rawDoc.DocumentType, RoomPhotoDocument.GalleryStateDocumentType, StringComparison.OrdinalIgnoreCase))
                {
                    return rawDoc.ToDomain();
                }

                return null;
            }

            var wasCover = targetDoc.IsCover;
            var targetSnapshot = targetDoc.ToDomain();
            var targetEtag = targetDoc.ETag;

            targetDoc.IsDeleted = true;
            targetDoc.IsCover = false;

            var remainingDocs = existingDocs
                .Where(d => !string.Equals(d.Id, normalizedPhotoId, StringComparison.Ordinal))
                .OrderBy(d => d.DisplayOrder)
                .ThenBy(d => d.UploadedAtUtc)
                .ToList();

            RoomPhotoDocument? promotedCover = null;
            string? promotedCoverEtag = null;
            if (wasCover && remainingDocs.Count > 0)
            {
                var nextCover = remainingDocs[0];
                if (!nextCover.IsCover)
                {
                    promotedCoverEtag = nextCover.ETag;
                    nextCover.IsCover = true;
                    promotedCover = nextCover;
                }
            }

            var updatedState = new RoomPhotoDocument
            {
                Id = stateId,
                RoomId = normalizedRoomId,
                DocumentType = RoomPhotoDocument.GalleryStateDocumentType,
                PhotoCount = remainingDocs.Count,
                NextDisplayOrder = remainingDocs.Count == 0
                    ? stateDoc?.NextDisplayOrder ?? 0
                    : Math.Max(remainingDocs.Max(d => d.DisplayOrder) + 1, stateDoc?.NextDisplayOrder ?? 0),
                CoverPhotoId = remainingDocs.FirstOrDefault(d => d.IsCover)?.Id,
                StateVersion = (stateDoc?.StateVersion ?? 0) + 1
            };

            var batch = new List<CosmosBatchOperation<RoomPhotoDocument>>
            {
                stateDoc is null
                    ? CosmosBatchOperation<RoomPhotoDocument>.Create(stateId, updatedState)
                    : CosmosBatchOperation<RoomPhotoDocument>.Replace(stateId, updatedState, stateDoc.ETag),
                CosmosBatchOperation<RoomPhotoDocument>.Replace(targetDoc.Id, targetDoc, targetEtag)
            };

            if (promotedCover is not null)
            {
                batch.Add(CosmosBatchOperation<RoomPhotoDocument>.Replace(promotedCover.Id, promotedCover, promotedCoverEtag));
            }

            try
            {
                await _dbContext.RoomPhotos.ExecuteTransactionalBatchAsync(normalizedRoomId, batch, cancellationToken);
                return targetSnapshot;
            }
            catch (CosmosException ex) when (IsRetryableConcurrencyStatus(ex.StatusCode))
            {
                if (attempt < MaxConcurrencyRetries)
                {
                    continue;
                }

                throw new ConcurrencyConflictException(
                    $"Photo '{normalizedPhotoId}' in room '{normalizedRoomId}' could not be deleted due to concurrent updates. Please retry.");
            }
        }

        throw new ConcurrencyConflictException(
            $"Photo '{normalizedPhotoId}' in room '{normalizedRoomId}' could not be deleted due to concurrent updates. Please retry.");
    }

    public async Task<IReadOnlyList<RoomPhoto>> GetPendingDeletionAsync(
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
            var pending = await inMemory.QueryAsync(
                p => p.IsDeleted &&
                     !string.Equals(p.DocumentType, RoomPhotoDocument.GalleryStateDocumentType, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(p.RoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

            return pending.Select(p => p.ToDomain()).ToList();
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.roomId = @roomId AND c.isDeleted = true AND (NOT IS_DEFINED(c.documentType) OR c.documentType = @docType)")
            .WithParameter("@roomId", normalizedRoomId)
            .WithParameter("@docType", RoomPhotoDocument.PhotoDocumentType);

        var docs = await _dbContext.RoomPhotos.QueryAsync(query, cancellationToken);
        return docs.Select(p => p.ToDomain()).ToList();
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

    private async Task<List<RoomPhotoDocument>> GetActivePhotoDocumentsAsync(
        string normalizedRoomId,
        CancellationToken cancellationToken)
    {
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
            .ToList();
    }

    private static bool IsRetryableConcurrencyStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict or HttpStatusCode.NotFound;

    private static string BuildGalleryStateDocumentId(string normalizedRoomId) =>
        $"__gallery_state__:{normalizedRoomId}";
}
