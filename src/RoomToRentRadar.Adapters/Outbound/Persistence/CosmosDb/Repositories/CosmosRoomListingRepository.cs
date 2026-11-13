namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Mappings;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

public sealed class CosmosRoomListingRepository : IRoomListingRepository
{
    private readonly Container _container;

    public CosmosRoomListingRepository(CosmosClient cosmosClient, IOptions<CosmosDbSettings> options)
    {
        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.RoomsContainerName);
    }

    public async Task<IReadOnlyList<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
        string campusId,
        CancellationToken cancellationToken = default)
    {
        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.campusId = @campusId AND c.status = @status AND c.verificationStatus = @verificationStatus")
            .WithParameter("@campusId", campusId)
            .WithParameter("@status", (int)ListingRoomStatus.Available)
            .WithParameter("@verificationStatus", (int)VerificationStatus.Verified);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(campusId)
        };

        var iterator = _container.GetItemQueryIterator<RoomListingDocument>(queryDefinition, requestOptions: queryOptions);
        var results = new List<RoomListing>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in response)
            {
                results.Add(doc.ToDomain());
            }
        }

        return results;
    }

    public async Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
    {
        var doc = roomListing.ToDocument();
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.CampusId), cancellationToken: cancellationToken);
    }
}
