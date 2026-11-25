using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;

public sealed class InMemoryCosmosContainer<T> : ICosmosContainer<T> where T : class
{
    private readonly ConcurrentDictionary<string, (string Json, string ETag)> _storage = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    /// <summary>
    /// Invoked after a document snapshot has been taken but before it is returned. This makes
    /// concurrency tests able to deterministically arrange two readers with the same ETag.
    /// </summary>
    public Func<string, Task>? OnReadAsync { get; set; }

    public Task<T> CreateItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_syncRoot)
        {
            var id = GetItemId(item);
            if (_storage.ContainsKey(id))
            {
                throw CreateConflictException($"Document with id '{id}' already exists.");
            }

            ValidateUniqueConstraints(item);

            var etag = Guid.NewGuid().ToString("N");
            SetItemETag(item, etag);

            var json = JsonConvert.SerializeObject(item);
            _storage[id] = (json, etag);
            return Task.FromResult(Clone(item));
        }
    }

    public Task<T> UpsertItemAsync(T item, string partitionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_syncRoot)
        {
            var id = GetItemId(item);
            ValidateUniqueConstraints(item, id);

            var etag = Guid.NewGuid().ToString("N");
            SetItemETag(item, etag);

            var json = JsonConvert.SerializeObject(item);
            _storage[id] = (json, etag);
            return Task.FromResult(Clone(item));
        }
    }

    public Task<T> ReplaceItemAsync(
        T item,
        string id,
        string partitionKey,
        string? ifMatchEtag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_syncRoot)
        {
            if (!_storage.TryGetValue(id, out var existing))
            {
                throw new CosmosException(
                    $"Document with id '{id}' was not found.",
                    HttpStatusCode.NotFound,
                    0,
                    Guid.NewGuid().ToString("N"),
                    1.0);
            }

            if (ifMatchEtag is not null && !string.Equals(existing.ETag, ifMatchEtag, StringComparison.Ordinal))
            {
                throw new CosmosException(
                    $"Precondition failed: ETag '{ifMatchEtag}' does not match current document ETag '{existing.ETag}'.",
                    HttpStatusCode.PreconditionFailed,
                    0,
                    Guid.NewGuid().ToString("N"),
                    1.0);
            }

            ValidateUniqueConstraints(item, id);

            var newEtag = Guid.NewGuid().ToString("N");
            SetItemETag(item, newEtag);

            var json = JsonConvert.SerializeObject(item);
            _storage[id] = (json, newEtag);
            return Task.FromResult(Clone(item));
        }
    }

    public async Task<T?> ReadItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        T? snapshot;
        lock (_syncRoot)
        {
            if (_storage.TryGetValue(id, out var entry))
            {
                snapshot = JsonConvert.DeserializeObject<T>(entry.Json)!;
                SetItemETag(snapshot, entry.ETag);
            }
            else
            {
                snapshot = null;
            }
        }

        if (OnReadAsync is not null)
        {
            await OnReadAsync(id);
        }

        return snapshot;
    }

    public Task DeleteItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _storage.TryRemove(id, out _);
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<T>> QueryAsync(
        QueryDefinition queryDefinition,
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            var items = _storage.Values
                .Select(entry =>
                {
                    var doc = JsonConvert.DeserializeObject<T>(entry.Json)!;
                    SetItemETag(doc, entry.ETag);
                    return doc;
                })
                .ToList();

            var queryText = queryDefinition.QueryText;

            if (typeof(T) == typeof(UserDocument))
            {
                var userItems = items.Cast<UserDocument>();
                if (queryText.Contains("normalizedEmail", StringComparison.OrdinalIgnoreCase))
                {
                    var emailParam = GetQueryParameter(queryDefinition, "@email");
                    if (emailParam is not null)
                    {
                        var filtered = userItems
                            .Where(u => string.Equals(u.NormalizedEmail, emailParam.ToString(), StringComparison.OrdinalIgnoreCase))
                            .Cast<T>()
                            .ToList();
                        return Task.FromResult<IReadOnlyList<T>>(filtered);
                    }
                }
            }
            else if (typeof(T) == typeof(RefreshTokenDocument))
            {
                var tokenItems = items.Cast<RefreshTokenDocument>();
                if (queryText.Contains("tokenHash", StringComparison.OrdinalIgnoreCase))
                {
                    var hashParam = GetQueryParameter(queryDefinition, "@tokenHash");
                    if (hashParam is not null)
                    {
                        var filtered = tokenItems
                            .Where(t => string.Equals(t.TokenHash, hashParam.ToString(), StringComparison.Ordinal))
                            .Cast<T>()
                            .ToList();
                        return Task.FromResult<IReadOnlyList<T>>(filtered);
                    }
                }
                else if (queryText.Contains("tokenFamily", StringComparison.OrdinalIgnoreCase))
                {
                    var familyParam = GetQueryParameter(queryDefinition, "@tokenFamily");
                    if (familyParam is not null)
                    {
                        var filtered = tokenItems
                            .Where(t => string.Equals(t.TokenFamily, familyParam.ToString(), StringComparison.OrdinalIgnoreCase))
                            .Cast<T>()
                            .ToList();
                        return Task.FromResult<IReadOnlyList<T>>(filtered);
                    }
                }
            }
            else if (typeof(T) == typeof(FavouriteDocument))
            {
                var favItems = items.Cast<FavouriteDocument>();
                if (queryText.Contains("userId", StringComparison.OrdinalIgnoreCase))
                {
                    var userIdParam = GetQueryParameter(queryDefinition, "@userId");
                    if (userIdParam is not null)
                    {
                        var filtered = favItems
                            .Where(f => string.Equals(f.UserId, userIdParam.ToString(), StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(f => f.CreatedAtUtc)
                            .ThenBy(f => f.RoomId, StringComparer.Ordinal)
                            .Cast<T>()
                            .ToList();
                        return Task.FromResult<IReadOnlyList<T>>(filtered);
                    }
                }
            }
            else if (typeof(T) == typeof(RoomHouseRulesDocument))
            {
                var rulesItems = items.Cast<RoomHouseRulesDocument>();
                var roomIdParam = GetQueryParameter(queryDefinition, "@roomId")?.ToString();
                var docTypeParam = GetQueryParameter(queryDefinition, "@documentType")?.ToString();

                if (roomIdParam is not null)
                {
                    rulesItems = rulesItems.Where(r => string.Equals(r.RoomId, roomIdParam, StringComparison.Ordinal));
                }

                if (docTypeParam is not null)
                {
                    rulesItems = rulesItems.Where(r => string.Equals(r.DocumentType, docTypeParam, StringComparison.Ordinal));
                }

                return Task.FromResult<IReadOnlyList<T>>(rulesItems.Cast<T>().ToList());
            }
            else if (typeof(T) == typeof(ListingReportDocument))
            {
                var reportItems = items.Cast<ListingReportDocument>();
                var roomIdParam = GetQueryParameter(queryDefinition, "@roomId")?.ToString();
                var reporterParam = GetQueryParameter(queryDefinition, "@reporterUserId")?.ToString();
                var statusParam = GetQueryParameter(queryDefinition, "@status");
                var pendingParam = GetQueryParameter(queryDefinition, "@pendingStatus");
                var inReviewParam = GetQueryParameter(queryDefinition, "@inReviewStatus");

                if (roomIdParam is not null)
                {
                    reportItems = reportItems.Where(r => string.Equals(r.RoomId, roomIdParam, StringComparison.Ordinal));
                }

                if (reporterParam is not null && Guid.TryParse(reporterParam, out var reporterGuid))
                {
                    reportItems = reportItems.Where(r => r.ReporterUserId == reporterGuid);
                }

                if (statusParam is not null)
                {
                    var statusInt = Convert.ToInt32(statusParam);
                    reportItems = reportItems.Where(r => r.Status == statusInt);
                }
                else if (pendingParam is not null && inReviewParam is not null)
                {
                    var pendingInt = Convert.ToInt32(pendingParam);
                    var inReviewInt = Convert.ToInt32(inReviewParam);
                    reportItems = reportItems.Where(r => r.Status == pendingInt || r.Status == inReviewInt);
                }

                return Task.FromResult<IReadOnlyList<T>>(reportItems.Cast<T>().ToList());
            }

            return Task.FromResult<IReadOnlyList<T>>(items);
        }
    }

    public Task<IReadOnlyList<T>> QueryAsync(
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            var items = _storage.Values
                .Select(entry =>
                {
                    var doc = JsonConvert.DeserializeObject<T>(entry.Json)!;
                    SetItemETag(doc, entry.ETag);
                    return doc;
                })
                .Where(predicate)
                .ToList();

            return Task.FromResult<IReadOnlyList<T>>(items);
        }
    }

    public Task ExecuteBatchAsync(
        string partitionKey,
        IReadOnlyList<CosmosBatchOperation<T>> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(operations);

        if (operations.Count == 0)
        {
            return Task.CompletedTask;
        }

        lock (_syncRoot)
        {
            var staged = new Dictionary<string, (string Json, string ETag)>(_storage, StringComparer.Ordinal);
            var stagedItems = new List<(T Item, string Id, string NewEtag)>(operations.Count);

            foreach (var op in operations)
            {
                ArgumentNullException.ThrowIfNull(op.Item);
                var id = string.IsNullOrWhiteSpace(op.Id) ? GetItemId(op.Item) : op.Id;

                switch (op.Type)
                {
                    case CosmosBatchOperationType.Create:
                        if (staged.ContainsKey(id))
                        {
                            throw CreateConflictException($"Document with id '{id}' already exists.");
                        }

                        ValidateUniqueConstraints(op.Item);
                        break;

                    case CosmosBatchOperationType.Upsert:
                        ValidateUniqueConstraints(op.Item, id);
                        break;

                    case CosmosBatchOperationType.Replace:
                        if (!staged.TryGetValue(id, out var existing))
                        {
                            throw new CosmosException(
                                $"Document with id '{id}' was not found.",
                                HttpStatusCode.NotFound,
                                0,
                                Guid.NewGuid().ToString("N"),
                                1.0);
                        }

                        if (op.IfMatchEtag is not null &&
                            !string.Equals(existing.ETag, op.IfMatchEtag, StringComparison.Ordinal))
                        {
                            throw new CosmosException(
                                $"Precondition failed: ETag '{op.IfMatchEtag}' does not match current document ETag '{existing.ETag}'.",
                                HttpStatusCode.PreconditionFailed,
                                0,
                                Guid.NewGuid().ToString("N"),
                                1.0);
                        }

                        ValidateUniqueConstraints(op.Item, id);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(operations), op.Type, "Unsupported batch operation type.");
                }

                var newEtag = Guid.NewGuid().ToString("N");
                var clone = Clone(op.Item);
                SetItemETag(clone, newEtag);
                var json = JsonConvert.SerializeObject(clone);
                staged[id] = (json, newEtag);
                stagedItems.Add((op.Item, id, newEtag));
            }

            foreach (var (item, id, newEtag) in stagedItems)
            {
                SetItemETag(item, newEtag);
                _storage[id] = staged[id];
            }

            return Task.CompletedTask;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _storage.Clear();
        }
    }

    private void ValidateUniqueConstraints(T item, string? currentId = null)
    {
        if (item is RefreshTokenDocument tokenDoc)
        {
            var allDocs = _storage.Values
                .Select(entry => JsonConvert.DeserializeObject<T>(entry.Json)!)
                .Where(d => currentId is null || GetItemId(d) != currentId)
                .Cast<RefreshTokenDocument>();

            if (allDocs.Any(t => string.Equals(t.TokenHash, tokenDoc.TokenHash, StringComparison.Ordinal)))
            {
                throw CreateConflictException($"Unique index violation: RefreshToken with hash '{tokenDoc.TokenHash}' already exists.");
            }
        }
        else if (item is FavouriteDocument favDoc)
        {
            var allDocs = _storage.Values
                .Select(entry => JsonConvert.DeserializeObject<T>(entry.Json)!)
                .Where(d => currentId is null || GetItemId(d) != currentId)
                .Cast<FavouriteDocument>();

            if (allDocs.Any(f =>
                string.Equals(f.UserId, favDoc.UserId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(f.RoomId, favDoc.RoomId, StringComparison.OrdinalIgnoreCase)))
            {
                throw CreateConflictException(
                    $"Unique index violation: Favourite for user '{favDoc.UserId}' and room '{favDoc.RoomId}' already exists.");
            }
        }
    }

    private static string GetItemId(T item)
    {
        var idProp = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        var val = idProp?.GetValue(item)?.ToString();
        return val ?? Guid.NewGuid().ToString();
    }

    private static void SetItemETag(T item, string etag)
    {
        var etagProp = typeof(T).GetProperty("ETag", BindingFlags.Public | BindingFlags.Instance);
        if (etagProp?.CanWrite == true)
        {
            etagProp.SetValue(item, etag);
        }
    }

    private static object? GetQueryParameter(QueryDefinition query, string name)
    {
        foreach (var (paramName, paramValue) in query.GetQueryParameters())
        {
            if (string.Equals(paramName, name, StringComparison.OrdinalIgnoreCase))
            {
                return paramValue;
            }
        }

        return null;
    }

    private static CosmosException CreateConflictException(string message)
    {
        return new CosmosException(
            message,
            HttpStatusCode.Conflict,
            0,
            Guid.NewGuid().ToString("N"),
            1.0);
    }

    private static T Clone(T item)
    {
        var json = JsonConvert.SerializeObject(item);
        return JsonConvert.DeserializeObject<T>(json)!;
    }
}
