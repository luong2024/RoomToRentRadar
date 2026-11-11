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
}
