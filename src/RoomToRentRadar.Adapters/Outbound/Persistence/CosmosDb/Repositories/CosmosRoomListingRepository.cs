namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Mappings;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Application.DTOs;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

public sealed class CosmosRoomListingRepository : IRoomListingRepository
{
    private const int MaxWriteAttempts = 3;
    private const string UpsertOperation = "upsert";
    private const string DeleteOperation = "delete";
    private readonly Container _listingsContainer;
    private readonly Container _roomsContainer;
    private readonly Container _syncOutboxContainer;
    private readonly ILogger<CosmosRoomListingRepository> _logger;

    /// <summary>
    /// Cumulative Request Charge (RU) consumed by the most recent query operation.
    /// Useful for telemetry, diagnostics, and performance integration testing.
    /// </summary>
    public double LastRequestCharge { get; private set; }

    [ActivatorUtilitiesConstructor]
    public CosmosRoomListingRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosRoomListingRepository>? logger = null)
        : this(
            cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.ContainerName),
            cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.RoomsContainerName),
            cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.RoomProjectionSyncOutboxContainerName),
            logger)
    {
    }

    public CosmosRoomListingRepository(
        Container listingsContainer,
        Container roomsContainer,
        Container syncOutboxContainer,
        ILogger<CosmosRoomListingRepository>? logger = null)
    {
        _listingsContainer = listingsContainer ?? throw new ArgumentNullException(nameof(listingsContainer));
        _roomsContainer = roomsContainer ?? throw new ArgumentNullException(nameof(roomsContainer));
        _syncOutboxContainer = syncOutboxContainer ?? throw new ArgumentNullException(nameof(syncOutboxContainer));
        _logger = logger ?? NullLogger<CosmosRoomListingRepository>.Instance;
    }

    public async Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
        string campusId,
        GeoLocation campusLocation,
        int page,
        int pageSize,
        string sortBy,
        bool descending,
        CancellationToken cancellationToken = default)
    {
        var totalRu = 0.0;
        var filter = "c.campusId = @campusId AND c.status = @status AND c.verificationStatus = @verificationStatus AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)";
        var countDefinition = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {filter}")
            .WithParameter("@campusId", campusId)
            .WithParameter("@status", (int)ListingRoomStatus.Available)
            .WithParameter("@verificationStatus", (int)VerificationStatus.Verified);
        using var countIterator = _listingsContainer.GetItemQueryIterator<int>(countDefinition, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(campusId)
        });
        var totalCount = 0;
        if (countIterator.HasMoreResults)
        {
            var countPage = await countIterator.ReadNextAsync(cancellationToken);
            totalRu += countPage.RequestCharge;
            totalCount = countPage.FirstOrDefault();
        }

        var direction = descending ? "DESC" : "ASC";
        // NOTE ON PAGINATION STABILITY & COSMOS DB SPATIAL LIMITATION:
        // 1. For "price" and "area", queries include "c.id" as an explicit secondary tie-breaker in SQL
        //    (backed by Composite Indexes), guaranteeing 100% strict deterministic pagination across all pages.
        // 2. For "distance", the Azure Cosmos DB SQL Engine explicitly disallows combining ST_DISTANCE with any
        //    other properties in the ORDER BY clause. Attempting 'ORDER BY ST_DISTANCE(...), c.id' throws
        //    runtime BadRequestException: "Queries that have an ST_DISTANCE expression in the ORDER BY clause
        //    cannot contain any other properties in the ORDER BY clause."
        //    Consequently, distance sorting relies natively on Cosmos DB spatial indexing order.
        //    For high-concurrency infinite scroll with large clusters of duplicate coordinates,
        //    a cursor-based Continuation Token endpoint is recommended as a future enhancement.
        var (orderByClause, requiresLocationParam) = sortBy switch
        {
            "price" => ($"ORDER BY c.monthlyRentAmount {direction}, c.id {direction}", false),
            "area" => ($"ORDER BY c.areaSquareMeters {direction}, c.id {direction}", false),
            _ => ($"ORDER BY ST_DISTANCE(c.location, @campusLocation) {direction}", true)
        };

        var offset = checked((long)(page - 1) * pageSize);
        var queryDefinition = new QueryDefinition(
            $"SELECT * FROM c WHERE {filter} {orderByClause} OFFSET @offset LIMIT @limit")
            .WithParameter("@campusId", campusId)
            .WithParameter("@status", (int)ListingRoomStatus.Available)
            .WithParameter("@verificationStatus", (int)VerificationStatus.Verified)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);
        if (requiresLocationParam)
        {
            queryDefinition = queryDefinition.WithParameter("@campusLocation", new Point(campusLocation.Longitude, campusLocation.Latitude));
        }

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(campusId)
        };

        using var iterator = _listingsContainer.GetItemQueryIterator<RoomListingDocument>(queryDefinition, requestOptions: queryOptions);
        var results = new List<RoomListing>(pageSize);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            totalRu += response.RequestCharge;
            foreach (var doc in response)
            {
                results.Add(doc.ToDomain());
            }
        }

        LastRequestCharge = totalRu;
        return new PagedResult<RoomListing>(results, page, pageSize, totalCount);
    }

    public async Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roomListing);

        var doc = roomListing.ToDocument();
        var intent = await CreateIntentAsync(roomListing.Id, doc.CampusId, UpsertOperation, doc, cancellationToken);
        await SynchronizeUpsertAsync(doc, intent.Sequence, intent.PreviousCampusIds, cancellationToken);
        await CompleteIntentAsync(intent, cancellationToken);
    }

    public async Task DeleteAsync(string roomId, string campusId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(campusId);

        var intent = await CreateIntentAsync(roomId, campusId, DeleteOperation, null, cancellationToken);
        await SynchronizeDeleteAsync(roomId, campusId, intent.PreviousCampusIds, intent.Sequence, cancellationToken);
        await CompleteIntentAsync(intent, cancellationToken);
    }

    public async Task ReconcilePendingAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.state = @state")
            .WithParameter("@state", RoomProjectionSyncDocument.PendingState);
        using var iterator = _syncOutboxContainer.GetItemQueryIterator<RoomProjectionSyncDocument>(query);

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var intent in page)
            {
                try
                {
                    if (string.Equals(intent.Operation, UpsertOperation, StringComparison.Ordinal))
                    {
                        if (intent.Listing is null)
                        {
                            _logger.LogError("Cannot reconcile upsert intent {IntentId} for room {RoomId} because its listing snapshot is missing.", intent.Id, intent.RoomId);
                            continue;
                        }

                        await SynchronizeUpsertAsync(intent.Listing, intent.Sequence, intent.PreviousCampusIds, cancellationToken);
                    }
                    else if (string.Equals(intent.Operation, DeleteOperation, StringComparison.Ordinal))
                    {
                        await SynchronizeDeleteAsync(intent.RoomId, intent.CampusId, intent.PreviousCampusIds, intent.Sequence, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError("Ignoring room projection sync intent {IntentId} with unknown operation {Operation}.", intent.Id, intent.Operation);
                        continue;
                    }

                    await CompleteIntentAsync(intent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconcile room projection for room {RoomId}; intent {IntentId} will be retried.", intent.RoomId, intent.Id);
                }
            }
        }
    }

    private async Task<RoomDocument?> ReadDetailOrNullAsync(string roomId, CancellationToken cancellationToken)
    {
        return await ReadOrNullAsync<RoomDocument>(_roomsContainer, roomId, new PartitionKey(roomId), cancellationToken);
    }

    private async Task<T?> ReadOrNullAsync<T>(Container container, string id, PartitionKey partitionKey, CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await container.ReadItemAsync<T>(id, partitionKey, cancellationToken: cancellationToken);
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < MaxWriteAttempts)
            {
                _logger.LogWarning(ex, "Transient Cosmos read failure for room {RoomId}; retrying attempt {Attempt} of {MaxAttempts}.", id, attempt, MaxWriteAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
        }
    }

    private async Task SynchronizeUpsertAsync(
        RoomListingDocument listing,
        long sequence,
        IReadOnlyList<string>? previousCampusIds,
        CancellationToken cancellationToken)
    {
        await WriteProjectionIfNewerAsync(_listingsContainer, listing, listing.Id, new PartitionKey(listing.CampusId), sequence, cancellationToken);
        if (previousCampusIds is not null)
        {
            foreach (var previousCampusId in previousCampusIds)
            {
                if (!string.IsNullOrWhiteSpace(previousCampusId) &&
                    !string.Equals(previousCampusId, listing.CampusId, StringComparison.Ordinal))
                {
                    await DeleteProjectionIfNotNewerAsync(
                        _listingsContainer,
                        new RoomListingDocument { Id = listing.Id, CampusId = previousCampusId },
                        listing.Id,
                        new PartitionKey(previousCampusId),
                        sequence,
                        cancellationToken);
                }
            }
        }
        await SynchronizeDetailProjectionAsync(listing, sequence, cancellationToken);
    }

    private async Task SynchronizeDetailProjectionAsync(RoomListingDocument listing, long sequence, CancellationToken cancellationToken)
    {
        var existingDetail = await ReadDetailOrNullAsync(listing.Id, cancellationToken);
        var detail = RoomDocument.MergeListingFields(listing, existingDetail);
        await WriteProjectionIfNewerAsync(_roomsContainer, detail, detail.Id, new PartitionKey(detail.Id), sequence, cancellationToken);
    }

    private async Task SynchronizeDeleteAsync(
        string roomId,
        string campusId,
        IReadOnlyList<string>? previousCampusIds,
        long sequence,
        CancellationToken cancellationToken)
    {
        await DeleteProjectionIfNotNewerAsync(_roomsContainer, new RoomDocument { Id = roomId }, roomId, new PartitionKey(roomId), sequence, cancellationToken);
        await DeleteProjectionIfNotNewerAsync(_listingsContainer, new RoomListingDocument { Id = roomId, CampusId = campusId }, roomId, new PartitionKey(campusId), sequence, cancellationToken);

        if (previousCampusIds is not null)
        {
            foreach (var previousCampusId in previousCampusIds)
            {
                if (!string.IsNullOrWhiteSpace(previousCampusId) &&
                    !string.Equals(previousCampusId, campusId, StringComparison.Ordinal))
                {
                    await DeleteProjectionIfNotNewerAsync(
                        _listingsContainer,
                        new RoomListingDocument { Id = roomId, CampusId = previousCampusId },
                        roomId,
                        new PartitionKey(previousCampusId),
                        sequence,
                        cancellationToken);
                }
            }
        }
    }

    private static List<string> DeterminePreviousCampusIds(
        RoomProjectionSyncDocument? existing,
        string currentCampusId)
    {
        var cleanupCampusIds = new HashSet<string>(StringComparer.Ordinal);

        if (existing is not null)
        {
            // If the prior intent is still pending, inherit all cleanup campuses that haven't completed yet
            if (string.Equals(existing.State, RoomProjectionSyncDocument.PendingState, StringComparison.Ordinal))
            {
                if (existing.PreviousCampusIds is not null)
                {
                    foreach (var prev in existing.PreviousCampusIds)
                    {
                        if (!string.IsNullOrWhiteSpace(prev) && !string.Equals(prev, currentCampusId, StringComparison.Ordinal))
                        {
                            cleanupCampusIds.Add(prev);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(existing.PreviousCampusId) &&
                    !string.Equals(existing.PreviousCampusId, currentCampusId, StringComparison.Ordinal))
                {
                    cleanupCampusIds.Add(existing.PreviousCampusId);
                }
            }

            // If the room changed campus relative to the existing intent's primary campus, that campus needs cleanup
            if (!string.IsNullOrWhiteSpace(existing.CampusId) &&
                !string.Equals(existing.CampusId, currentCampusId, StringComparison.Ordinal))
            {
                cleanupCampusIds.Add(existing.CampusId);
            }
        }

        return cleanupCampusIds.ToList();
    }

    private async Task<RoomProjectionSyncDocument> CreateIntentAsync(
        string roomId,
        string campusId,
        string operation,
        RoomListingDocument? listing,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            var existing = await ReadOrNullAsync<RoomProjectionSyncDocument>(
                _syncOutboxContainer,
                roomId,
                new PartitionKey(roomId),
                cancellationToken);

            var previousCampusIds = DeterminePreviousCampusIds(existing, campusId);

            var intent = new RoomProjectionSyncDocument
            {
                Id = roomId,
                RoomId = roomId,
                CampusId = campusId,
                PreviousCampusIds = previousCampusIds,
                Operation = operation,
                Listing = listing,
                Sequence = (existing?.Sequence ?? 0) + 1,
                State = RoomProjectionSyncDocument.PendingState,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                var response = existing is null
                    ? await ExecuteWriteAsync(roomId, "create-projection-sync-intent", () => _syncOutboxContainer.CreateItemAsync(intent, new PartitionKey(intent.Id), cancellationToken: cancellationToken), cancellationToken)
                    : await ExecuteWriteAsync(roomId, "replace-projection-sync-intent", () => _syncOutboxContainer.ReplaceItemAsync(intent, intent.Id, new PartitionKey(intent.Id), new ItemRequestOptions { IfMatchEtag = existing.ETag }, cancellationToken), cancellationToken);
                intent.ETag = response.ETag;
                return intent;
            }
            catch (CosmosException ex) when ((ex.StatusCode == System.Net.HttpStatusCode.Conflict || ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed) && attempt < MaxWriteAttempts)
            {
                _logger.LogInformation("Projection sync intent changed for room {RoomId}; retrying intent creation.", roomId);
            }
        }

        throw new InvalidOperationException($"Could not create a projection sync intent for room '{roomId}'.");
    }

    private async Task CompleteIntentAsync(RoomProjectionSyncDocument intent, CancellationToken cancellationToken)
    {
        try
        {
            intent.State = RoomProjectionSyncDocument.CompletedState;
            await ExecuteWriteAsync(intent.RoomId, "complete-projection-sync-intent", () => _syncOutboxContainer.ReplaceItemAsync(intent, intent.Id, new PartitionKey(intent.Id), new ItemRequestOptions { IfMatchEtag = intent.ETag }, cancellationToken), cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            _logger.LogInformation("A newer projection sync intent exists for room {RoomId}; leaving it pending for reconciliation.", intent.RoomId);
        }
    }

    private async Task WriteProjectionIfNewerAsync<T>(
        Container container,
        T candidate,
        string id,
        PartitionKey partitionKey,
        long sequence,
        CancellationToken cancellationToken)
        where T : class, IProjectionVersionedDocument
    {
        candidate.ProjectionVersion = sequence;
        candidate.IsDeleted = false;
        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            var existing = await ReadOrNullAsync<T>(container, id, partitionKey, cancellationToken);
            if (existing is not null && existing.ProjectionVersion >= sequence)
            {
                return;
            }

            try
            {
                if (existing is null)
                {
                    await ExecuteWriteAsync(id, "create-projection", () => container.CreateItemAsync(candidate, partitionKey, cancellationToken: cancellationToken), cancellationToken);
                }
                else
                {
                    await ExecuteWriteAsync(id, "replace-projection", () => container.ReplaceItemAsync(candidate, id, partitionKey, new ItemRequestOptions { IfMatchEtag = existing.ETag }, cancellationToken), cancellationToken);
                }

                return;
            }
            catch (CosmosException ex) when ((ex.StatusCode == System.Net.HttpStatusCode.Conflict || ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed) && attempt < MaxWriteAttempts)
            {
                _logger.LogInformation("Projection changed for room {RoomId}; retrying fenced write.", id);
            }
        }

        throw new InvalidOperationException($"Could not write the current projection for room '{id}'.");
    }

    private async Task DeleteProjectionIfNotNewerAsync<T>(
        Container container,
        T tombstone,
        string id,
        PartitionKey partitionKey,
        long sequence,
        CancellationToken cancellationToken)
        where T : class, IProjectionVersionedDocument
    {
        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            var existing = await ReadOrNullAsync<T>(container, id, partitionKey, cancellationToken);
            if (existing is not null && existing.ProjectionVersion >= sequence)
            {
                return;
            }

            try
            {
                var projection = existing ?? tombstone;
                projection.ProjectionVersion = sequence;
                projection.IsDeleted = true;
                if (existing is null)
                {
                    await ExecuteWriteAsync(id, "create-projection-tombstone", () => container.CreateItemAsync(projection, partitionKey, cancellationToken: cancellationToken), cancellationToken);
                }
                else
                {
                    await ExecuteWriteAsync(id, "replace-projection-tombstone", () => container.ReplaceItemAsync(projection, id, partitionKey, new ItemRequestOptions { IfMatchEtag = existing.ETag }, cancellationToken), cancellationToken);
                }
                return;
            }
            catch (CosmosException ex) when ((ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed) && attempt < MaxWriteAttempts)
            {
                _logger.LogInformation("Projection changed for room {RoomId}; retrying fenced delete.", id);
            }
        }

        throw new InvalidOperationException($"Could not delete the current projection for room '{id}'.");
    }

    private async Task ExecuteWriteAsync(
        string roomId,
        string operation,
        Func<Task> write,
        CancellationToken cancellationToken)
    {
        await ExecuteWriteAsync(
            roomId,
            operation,
            async () =>
            {
                await write();
                return true;
            },
            cancellationToken);
    }

    private async Task<T> ExecuteWriteAsync<T>(
        string roomId,
        string operation,
        Func<Task<T>> write,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await write();
            }
            catch (CosmosException ex) when (IsTransient(ex) && attempt < MaxWriteAttempts)
            {
                _logger.LogWarning(ex,
                    "Transient Cosmos failure while {Operation} for room {RoomId}; retrying attempt {Attempt} of {MaxAttempts}.",
                    operation, roomId, attempt, MaxWriteAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to {Operation} for room {RoomId}; projections may require reconciliation.",
                    operation, roomId);
                throw;
            }
        }
    }

    private static bool IsTransient(CosmosException exception) =>
        exception.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
        exception.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
        (int)exception.StatusCode >= 500;
}
