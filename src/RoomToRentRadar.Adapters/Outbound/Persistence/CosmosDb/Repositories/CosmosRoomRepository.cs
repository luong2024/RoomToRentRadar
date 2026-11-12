using System.Net;
using Microsoft.Azure.Cosmos;
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
            ItemResponse<Room> response = await _container.ReadItemAsync<Room>(
                id: id,
                partitionKey: new PartitionKey(id),
                cancellationToken: cancellationToken);

            return response.Resource;
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

        using var iterator = _container.GetItemQueryIterator<Room>(query);
        var results = new List<Room>();

        while (iterator.HasMoreResults && results.Count < 5)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}
