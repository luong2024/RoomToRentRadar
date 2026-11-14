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

    /// <summary>
    /// Cumulative Request Charge (RU) consumed by the most recent query operation.
    /// Useful for telemetry, diagnostics, and performance integration testing.
    /// </summary>
    public double LastRequestCharge { get; private set; }

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
        var totalRu = 0.0;
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
            totalRu += countPage.RequestCharge;
            totalCount = countPage.FirstOrDefault();
        }

        var direction = descending ? "DESC" : "ASC";
        // Stable pagination: include c.id as a tie-breaker for deterministic pagination across pages
        var (orderByClause, requiresLocationParam) = sortBy switch
        {
            "price" => ($"ORDER BY c.monthlyRentAmount {direction}, c.id {direction}", false),
            "area" => ($"ORDER BY c.areaSquareMeters {direction}, c.id {direction}", false),
            _ => ($"ORDER BY ST_DISTANCE(c.location, @campusLocation) {direction}", true)
        };

        var queryDefinition = new QueryDefinition(
            $"SELECT * FROM c WHERE {filter} {orderByClause} OFFSET @offset LIMIT @limit")
            .WithParameter("@campusId", campusId)
            .WithParameter("@status", (int)ListingRoomStatus.Available)
            .WithParameter("@verificationStatus", (int)VerificationStatus.Verified)
            .WithParameter("@offset", (page - 1) * pageSize)
            .WithParameter("@limit", pageSize);
        if (requiresLocationParam)
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
            totalRu += response.RequestCharge;
            foreach (var doc in response)
            {
                results.Add(doc.ToDomain());
            }
        }

        LastRequestCharge = totalRu;
        return new PagedResult<RoomListing>(results, page, pageSize, totalCount);
    }

    public async Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
    {
        var doc = roomListing.ToDocument();
        await _container.UpsertItemAsync(doc, new PartitionKey(doc.CampusId), cancellationToken: cancellationToken);
    }
}
