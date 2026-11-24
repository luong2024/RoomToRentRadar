using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public class CosmosRoomRepository : IRoomRepository
{
    private readonly Container _container;

    public CosmosRoomRepository(Container container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public async Task<Room> CreateAsync(Room room, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        var document = RoomDocument.FromDomain(room);
        var response = await _container.CreateItemAsync(document, new PartitionKey(document.Id), cancellationToken: cancellationToken);
        return response.Resource.ToDomain();
    }

    public async Task UpdateAsync(Room room, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        var existing = await ReadDocumentOrNullAsync(room.Id, cancellationToken);
        if (existing is null || existing.IsDeleted)
        {
            throw new NotFoundException($"Room '{room.Id}' was not found.");
        }

        var expectedETag = string.IsNullOrWhiteSpace(room.ConcurrencyToken)
            ? existing.ETag
            : room.ConcurrencyToken;

        var document = RoomDocument.FromDomain(room);
        try
        {
            await _container.ReplaceItemAsync(
                document,
                document.Id,
                new PartitionKey(document.Id),
                new ItemRequestOptions { IfMatchEtag = expectedETag },
                cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new ConcurrencyConflictException("The listing was changed by another request. Reload it and retry.", ex);
        }
    }

    private async Task<RoomDocument?> ReadDocumentOrNullAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<RoomDocument>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var trimmedId = id.Trim();

        try
        {
            ItemResponse<RoomDocument> response = await _container.ReadItemAsync<RoomDocument>(
                id: trimmedId,
                partitionKey: new PartitionKey(trimmedId),
                cancellationToken: cancellationToken);

            if (response?.Resource is { IsDeleted: false } document)
            {
                return document.ToDomain();
            }

            if (response?.Resource is { IsDeleted: true })
            {
                return null;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Fall through to case-insensitive lookup when exact-case point read misses
        }

        var fallbackQuery = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE STRINGEQUALS(c.id, @id, true) AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)")
            .WithParameter("@id", trimmedId);

        using var iterator = _container.GetItemQueryIterator<RoomDocument>(
            fallbackQuery,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        if (iterator is null)
        {
            return null;
        }

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var match = page.FirstOrDefault(d => !d.IsDeleted);
            if (match is not null)
            {
                return match.ToDomain();
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<Room>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<Room>();
        }

        var distinctIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return Array.Empty<Room>();
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id) AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)")
            .WithParameter("@ids", distinctIds);

        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = distinctIds.Length
        };

        var results = new List<Room>(distinctIds.Length);
        var foundIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var iterator = _container.GetItemQueryIterator<RoomDocument>(query, requestOptions: requestOptions))
        {
            if (iterator is not null)
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync(cancellationToken);
                    foreach (var doc in response)
                    {
                        if (!doc.IsDeleted && foundIds.Add(doc.Id))
                        {
                            results.Add(doc.ToDomain());
                        }
                    }
                }
            }
        }

        var unmatchedIds = distinctIds
            .Where(id => !foundIds.Contains(id))
            .ToArray();

        if (unmatchedIds.Length > 0)
        {
            var clauses = new List<string>(unmatchedIds.Length);
            for (var i = 0; i < unmatchedIds.Length; i++)
            {
                clauses.Add($"STRINGEQUALS(c.id, @id{i}, true)");
            }

            var fallbackSql = $"SELECT * FROM c WHERE ({string.Join(" OR ", clauses)}) AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)";
            var fallbackQuery = new QueryDefinition(fallbackSql);
            for (var i = 0; i < unmatchedIds.Length; i++)
            {
                fallbackQuery = fallbackQuery.WithParameter($"@id{i}", unmatchedIds[i]);
            }

            using var fallbackIterator = _container.GetItemQueryIterator<RoomDocument>(
                fallbackQuery,
                requestOptions: new QueryRequestOptions { MaxItemCount = unmatchedIds.Length });

            if (fallbackIterator is not null)
            {
                while (fallbackIterator.HasMoreResults)
                {
                    var response = await fallbackIterator.ReadNextAsync(cancellationToken);
                    foreach (var doc in response)
                    {
                        if (!doc.IsDeleted && foundIds.Add(doc.Id))
                        {
                            results.Add(doc.ToDomain());
                        }
                    }
                }
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default)
    {
        var minPrice = price * 0.7m;
        var maxPrice = price * 1.3m;
        var minArea = area * 0.7;
        var maxArea = area * 1.3;

        var query = new QueryDefinition(
            "SELECT TOP 5 * FROM c WHERE c.id != @roomId AND c.status = 0 AND c.isVerified = true AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) AND c.price >= @minPrice AND c.price <= @maxPrice AND c.area >= @minArea AND c.area <= @maxArea")
            .WithParameter("@roomId", roomId)
            .WithParameter("@minPrice", minPrice)
            .WithParameter("@maxPrice", maxPrice)
            .WithParameter("@minArea", minArea)
            .WithParameter("@maxArea", maxArea);

        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = 5
        };

        using var iterator = _container.GetItemQueryIterator<RoomDocument>(query, requestOptions: requestOptions);
        var results = new List<Room>();

        while (iterator.HasMoreResults && results.Count < 5)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in response)
            {
                results.Add(doc.ToDomain());
            }
        }

        return results;
    }

    public async Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(
        SearchRoomsQuery query,
        CancellationToken ct = default)
    {
        var whereClauses = new List<string>
        {
            "c.status = 0",
            "c.isVerified = true",
            "(NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)"
        };

        if (query.MinPrice.HasValue)
        {
            whereClauses.Add("c.price >= @minPrice");
        }

        if (query.MaxPrice.HasValue)
        {
            whereClauses.Add("c.price <= @maxPrice");
        }

        if (query.MinArea.HasValue)
        {
            whereClauses.Add("c.area >= @minArea");
        }

        if (query.MaxArea.HasValue)
        {
            whereClauses.Add("c.area <= @maxArea");
        }

        if (!string.IsNullOrWhiteSpace(query.RoomType))
        {
            whereClauses.Add("STRINGEQUALS(c.roomType, @roomType, true)");
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            whereClauses.Add("(CONTAINS(c.title, @keyword, true) OR CONTAINS(c.description, @keyword, true) OR CONTAINS(c.address, @keyword, true))");
        }

        if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusInKm.HasValue)
        {
            whereClauses.Add("(ST_DISTANCE(c.location, @location) <= (@radius * 1000) OR ST_DISTANCE(c.Location, @location) <= (@radius * 1000))");
        }

        if (query.Amenities is { Count: > 0 })
        {
            var requestedAmenities = query.Amenities
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .ToList();

            for (var i = 0; i < requestedAmenities.Count; i++)
            {
                whereClauses.Add($"ARRAY_CONTAINS(c.amenities, @amenity{i})");
            }
        }

        var whereSql = string.Join(" AND ", whereClauses);

        // Cross-partition count query with explicit QueryRequestOptions
        var countQueryDef = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {whereSql}");
        countQueryDef = BindParameters(countQueryDef, query);

        var countRequestOptions = new QueryRequestOptions
        {
            MaxItemCount = 1
        };

        var totalCount = 0;
        using var countIterator = _container.GetItemQueryIterator<int>(countQueryDef, requestOptions: countRequestOptions);
        while (countIterator.HasMoreResults)
        {
            var countResponse = await countIterator.ReadNextAsync(ct);
            totalCount = countResponse.FirstOrDefault();
        }

        if (totalCount == 0)
        {
            return (Array.Empty<Room>(), 0);
        }

        // Paged items query across partitions
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 1, 50);
        long rawOffset = ((long)pageNumber - 1) * pageSize;

        if (rawOffset >= totalCount || rawOffset > int.MaxValue)
        {
            return (Array.Empty<Room>(), totalCount);
        }

        var offset = (int)rawOffset;

        var normalizedSort = query.SortBy?.Trim().ToLowerInvariant();
        var orderByClause = normalizedSort switch
        {
            "priceasc" or "price_asc" => "ORDER BY c.price ASC, c.id ASC",
            "pricedesc" or "price_desc" => "ORDER BY c.price DESC, c.id DESC",
            "distanceasc" or "distance_asc" when query.Latitude.HasValue && query.Longitude.HasValue =>
                "ORDER BY ST_DISTANCE(c.location, @location) ASC",
            "newest" => "ORDER BY c.createdAt DESC, c.id DESC",
            _ => "ORDER BY c.id ASC"
        };

        var itemsSql = $"SELECT * FROM c WHERE {whereSql} {orderByClause} OFFSET @offset LIMIT @limit";
        var itemsQueryDef = new QueryDefinition(itemsSql);
        itemsQueryDef = BindParameters(itemsQueryDef, query);
        itemsQueryDef = itemsQueryDef.WithParameter("@offset", offset);
        itemsQueryDef = itemsQueryDef.WithParameter("@limit", pageSize);

        var itemsRequestOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        var items = new List<Room>();
        using var itemsIterator = _container.GetItemQueryIterator<RoomDocument>(itemsQueryDef, requestOptions: itemsRequestOptions);
        while (itemsIterator.HasMoreResults)
        {
            var page = await itemsIterator.ReadNextAsync(ct);
            foreach (var doc in page)
            {
                items.Add(doc.ToDomain());
            }
        }

        return (items, totalCount);
    }

    private static QueryDefinition BindParameters(QueryDefinition queryDef, SearchRoomsQuery query)
    {
        if (query.MinPrice.HasValue)
        {
            queryDef = queryDef.WithParameter("@minPrice", query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            queryDef = queryDef.WithParameter("@maxPrice", query.MaxPrice.Value);
        }

        if (query.MinArea.HasValue)
        {
            queryDef = queryDef.WithParameter("@minArea", query.MinArea.Value);
        }

        if (query.MaxArea.HasValue)
        {
            queryDef = queryDef.WithParameter("@maxArea", query.MaxArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.RoomType))
        {
            queryDef = queryDef.WithParameter("@roomType", query.RoomType);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryDef = queryDef.WithParameter("@keyword", query.Keyword.Trim());
        }

        if (query.Latitude.HasValue && query.Longitude.HasValue)
        {
            var locationPoint = new
            {
                type = "Point",
                coordinates = new[] { query.Longitude.Value, query.Latitude.Value }
            };
            queryDef = queryDef.WithParameter("@location", locationPoint);

            if (query.RadiusInKm.HasValue)
            {
                queryDef = queryDef.WithParameter("@radius", query.RadiusInKm.Value);
            }
        }

        if (query.Amenities is { Count: > 0 })
        {
            var requestedAmenities = query.Amenities
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .ToList();

            for (var i = 0; i < requestedAmenities.Count; i++)
            {
                queryDef = queryDef.WithParameter($"@amenity{i}", requestedAmenities[i]);
            }
        }

        return queryDef;
    }
}
