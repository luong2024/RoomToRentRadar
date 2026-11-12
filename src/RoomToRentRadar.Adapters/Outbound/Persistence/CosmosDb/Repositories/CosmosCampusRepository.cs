namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Mappings;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

public sealed class CosmosCampusRepository : ICampusRepository
{
    private readonly Container _container;

    public CosmosCampusRepository(CosmosClient cosmosClient, IOptions<CosmosDbSettings> options)
    {
        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.CampusesContainerName);
    }

    public async Task<Campus?> GetByIdAsync(string campusId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<CampusDocument>(
                campusId,
                new PartitionKey(campusId),
                cancellationToken: cancellationToken);

            return response.Resource?.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task AddAsync(Campus campus, CancellationToken cancellationToken = default)
    {
        var doc = campus.ToDocument();
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.Id), cancellationToken: cancellationToken);
    }
}
