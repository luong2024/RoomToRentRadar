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
