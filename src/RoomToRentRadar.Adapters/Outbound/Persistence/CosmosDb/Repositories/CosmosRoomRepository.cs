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

        var query = new QueryDefinition(
            "SELECT TOP 5 * FROM c WHERE c.id != @roomId AND c.status = 0 AND c.price >= @minPrice AND c.price <= @maxPrice")
            .WithParameter("@roomId", roomId)
            .WithParameter("@minPrice", minPrice)
            .WithParameter("@maxPrice", maxPrice);

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
