using System.Net;
using Microsoft.Azure.Cosmos;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;

public sealed class CosmosContainer<T> : ICosmosContainer<T> where T : class
{
    private readonly Container _container;

    public CosmosContainer(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
    }

    public async Task<T> CreateItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(
            item,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<T> UpsertItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default)
    {
        var response = await _container.UpsertItemAsync(
            item,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<T> ReplaceItemAsync(
        T item,
        string id,
        string partitionKey,
        string? ifMatchEtag = null,
        CancellationToken cancellationToken = default)
    {
        var requestOptions = ifMatchEtag is not null
            ? new ItemRequestOptions { IfMatchEtag = ifMatchEtag }
            : null;

        var response = await _container.ReplaceItemAsync(
            item,
            id,
            new PartitionKey(partitionKey),
            requestOptions,
            cancellationToken);

        return response.Resource;
    }

    public async Task<T?> ReadItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already deleted or absent
        }
    }

    public async Task ExecuteTransactionalBatchAsync(
        string partitionKey,
        IReadOnlyList<CosmosBatchOperation<T>> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(operations);

        if (operations.Count == 0)
        {
            return;
        }

        var batch = _container.CreateTransactionalBatch(new PartitionKey(partitionKey));
        foreach (var op in operations)
        {
            var options = op.IfMatchEtag is not null
                ? new TransactionalBatchItemRequestOptions { IfMatchEtag = op.IfMatchEtag }
                : null;

            switch (op.Type)
            {
                case CosmosBatchOperationType.Create:
                    batch.CreateItem(op.Item ?? throw new ArgumentException($"Create operation for '{op.Id}' requires an item."));
                    break;
                case CosmosBatchOperationType.Replace:
                    batch.ReplaceItem(op.Id, op.Item ?? throw new ArgumentException($"Replace operation for '{op.Id}' requires an item."), options);
                    break;
                case CosmosBatchOperationType.Delete:
                    batch.DeleteItem(op.Id, options);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operations), op.Type, "Unsupported transactional batch operation.");
            }
        }

        using var response = await batch.ExecuteAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CosmosException(
                response.ErrorMessage ?? $"Transactional batch failed with status {(int)response.StatusCode}.",
                response.StatusCode,
                0,
                response.ActivityId,
                response.RequestCharge);
        }
    }

    public async Task<IReadOnlyList<T>> QueryAsync(
        QueryDefinition queryDefinition,
        CancellationToken cancellationToken = default)
    {
        using var iterator = _container.GetItemQueryIterator<T>(queryDefinition);
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public Task<IReadOnlyList<T>> QueryAsync(
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("In-memory predicates are for the in-memory test container. Use QueryDefinition for Cosmos DB.");
    }
}
