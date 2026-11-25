using Microsoft.Azure.Cosmos;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;

public enum CosmosBatchOperationType
{
    Create,
    Replace,
    Delete
}

public sealed record CosmosBatchOperation<T>(
    CosmosBatchOperationType Type,
    string Id,
    T? Item = null,
    string? IfMatchEtag = null) where T : class
{
    public static CosmosBatchOperation<T> Create(string id, T item) =>
        new(CosmosBatchOperationType.Create, id, item);

    public static CosmosBatchOperation<T> Replace(string id, T item, string? ifMatchEtag = null) =>
        new(CosmosBatchOperationType.Replace, id, item, ifMatchEtag);

    public static CosmosBatchOperation<T> Delete(string id, string? ifMatchEtag = null) =>
        new(CosmosBatchOperationType.Delete, id, null, ifMatchEtag);
}

public interface ICosmosContainer<T> where T : class
{
    Task<T> CreateItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default);

    Task<T> UpsertItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default);

    Task<T> ReplaceItemAsync(
        T item,
        string id,
        string partitionKey,
        string? ifMatchEtag = null,
        CancellationToken cancellationToken = default);

    Task<T?> ReadItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task ExecuteTransactionalBatchAsync(
        string partitionKey,
        IReadOnlyList<CosmosBatchOperation<T>> operations,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync(QueryDefinition queryDefinition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default);
}
