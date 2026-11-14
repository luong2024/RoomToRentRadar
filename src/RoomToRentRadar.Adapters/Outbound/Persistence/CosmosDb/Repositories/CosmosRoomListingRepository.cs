namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Mappings;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Application.DTOs;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

public sealed class CosmosRoomListingRepository : IRoomListingRepository
{
    private readonly Container _container;

    public CosmosRoomListingRepository(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options)
    {
        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.ContainerName);
    }

    public async Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
        string campusId,
        GeoLocation campusLocation,
        int page,
        int pageSize,
        string sortBy,
        bool descending,
        CancellationToken cancellationToken = default)
    {
        var filter = "c.campusId = @campusId AND c.status = @status AND c.verificationStatus = @verificationStatus";
        var countDefinition = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {filter}")
            .WithParameter("@campusId", campusId)
            .WithParameter("@status", (int)ListingRoomStatus.Available)
            .WithParameter("@verificationStatus", (int)VerificationStatus.Verified);
        using var countIterator = _container.GetItemQueryIterator<int>(countDefinition, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(campusId)
        });
        var totalCount = 0;
        if (countIterator.HasMoreResults)
        {
            var countPage = await countIterator.ReadNextAsync(cancellationToken);
            totalCount = countPage.FirstOrDefault();
        }

        var orderBy = sortBy switch
        {
            "price" => "c.monthlyRentAmount",
            "area" => "c.areaSquareMeters",
            _ => "ST_DISTANCE(c.location, @campusLocation)"
        };
        var direction = descending ? "DESC" : "ASC";
        var queryDefinition = new QueryDefinition(
            $"SELECT * FROM c WHERE {filter} ORDER BY {orderBy} {direction} OFFSET @offset LIMIT @limit")
            .WithParameter("@campusId", campusId)
            .WithParameter("@status", (int)ListingRoomStatus.Available)
            .WithParameter("@verificationStatus", (int)VerificationStatus.Verified)
            .WithParameter("@offset", (page - 1) * pageSize)
            .WithParameter("@limit", pageSize);
        if (sortBy is not "price" and not "area")
        {
            queryDefinition = queryDefinition.WithParameter("@campusLocation", new Point(campusLocation.Longitude, campusLocation.Latitude));
        }

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(campusId)
        };

        using var iterator = _container.GetItemQueryIterator<RoomListingDocument>(queryDefinition, requestOptions: queryOptions);
        var results = new List<RoomListing>(pageSize);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in response)
            {
                results.Add(doc.ToDomain());
            }
        }

        return new PagedResult<RoomListing>(results, page, pageSize, totalCount);
    }

    public async Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
    {
        var doc = roomListing.ToDocument();
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.CampusId), cancellationToken: cancellationToken);
    }
}
