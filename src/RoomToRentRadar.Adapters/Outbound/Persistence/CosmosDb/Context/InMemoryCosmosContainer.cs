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

    public Task<T?> ReadItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (_storage.TryGetValue(id, out var entry))
            {
                var doc = JsonConvert.DeserializeObject<T>(entry.Json)!;
                SetItemETag(doc, entry.ETag);
                return Task.FromResult<T?>(doc);
            }

            return Task.FromResult<T?>(null);
        }
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

    public void Clear()
    {
        lock (_syncRoot)
        {
            _storage.Clear();
        }
    }

    private void ValidateUniqueConstraints(T item, string? currentId = null)
    {
        var allDocs = _storage.Values
            .Select(entry => JsonConvert.DeserializeObject<T>(entry.Json)!)
            .Where(d => currentId is null || GetItemId(d) != currentId)
            .ToList();

        if (item is UserDocument userDoc)
        {
            var userDocs = allDocs.Cast<UserDocument>();

            // Unique ExternalLogins (Provider, ProviderSubject)
            foreach (var el in userDoc.ExternalLogins)
            {
                if (userDocs.Any(u => u.ExternalLogins.Any(x =>
                    string.Equals(x.Provider, el.Provider, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ProviderSubject, el.ProviderSubject, StringComparison.Ordinal))))
                {
                    throw CreateConflictException($"Unique index violation: External login for provider '{el.Provider}' already linked to another user.");
                }
            }
        }
        else if (item is RefreshTokenDocument tokenDoc)
        {
            var tokenDocs = allDocs.Cast<RefreshTokenDocument>();
            if (tokenDocs.Any(t => string.Equals(t.TokenHash, tokenDoc.TokenHash, StringComparison.Ordinal)))
            {
                throw CreateConflictException($"Unique index violation: RefreshToken with hash '{tokenDoc.TokenHash}' already exists.");
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
        var field = typeof(QueryDefinition).GetField("parameters", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(query) is System.Collections.IEnumerable list)
        {
            foreach (var item in list)
            {
                var nameProp = item.GetType().GetProperty("Name");
                var valProp = item.GetType().GetProperty("Value");
                var paramName = nameProp?.GetValue(item)?.ToString();
                if (string.Equals(paramName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return valProp?.GetValue(item);
                }
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
