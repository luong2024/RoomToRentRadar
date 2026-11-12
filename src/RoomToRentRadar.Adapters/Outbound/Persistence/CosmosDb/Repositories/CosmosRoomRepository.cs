using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.DTOs;
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
        var response = await _container.CreateItemAsync(room, new PartitionKey(room.Id), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            ItemResponse<RoomDocument> response = await _container.ReadItemAsync<RoomDocument>(
                id: id,
                partitionKey: new PartitionKey(id),
                cancellationToken: cancellationToken);

            return response.Resource?.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default)
    {
        var minPrice = price * 0.7m;
        var maxPrice = price * 1.3m;
        var minArea = area * 0.7;
        var maxArea = area * 1.3;

        var query = new QueryDefinition(
            "SELECT TOP 5 * FROM c WHERE c.id != @roomId AND c.status = 0 AND c.price >= @minPrice AND c.price <= @maxPrice AND c.area >= @minArea AND c.area <= @maxArea")
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
            "c.isVerified = true"
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
            whereClauses.Add("ST_DISTANCE(c.Location, {'type': 'Point', 'coordinates': [@lon, @lat]}) <= (@radius * 1000)");
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
        var offset = (pageNumber - 1) * pageSize;

        var itemsSql = $"SELECT * FROM c WHERE {whereSql} ORDER BY c.id OFFSET @offset LIMIT @limit";
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
            queryDef = queryDef.WithParameter("@keyword", query.Keyword);
        }

        if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusInKm.HasValue)
        {
            queryDef = queryDef.WithParameter("@lon", query.Longitude.Value);
            queryDef = queryDef.WithParameter("@lat", query.Latitude.Value);
            queryDef = queryDef.WithParameter("@radius", query.RadiusInKm.Value);
        }

        return queryDef;
    }
}
