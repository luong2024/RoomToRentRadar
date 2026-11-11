using Microsoft.Azure.Cosmos;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;

public interface ICosmosContainer<T> where T : class
{
    Task<T> CreateItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default);

    Task<T> UpsertItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default);

    Task<T?> ReadItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync(QueryDefinition queryDefinition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default);
}
