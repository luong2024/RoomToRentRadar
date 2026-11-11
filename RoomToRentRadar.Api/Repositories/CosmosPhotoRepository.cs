using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Options;

namespace RoomToRentRadar.Api.Repositories;

public class CosmosPhotoRepository : IPhotoRepository
{
    private static readonly CosmosLinqSerializerOptions LinqOptions = new()
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    };

    private readonly Container _container;

    public CosmosPhotoRepository(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        var o = options.Value;
        _container = client.GetContainer(o.DatabaseName, o.PhotosContainer);
    }

    public async Task AddRangeAsync(IEnumerable<RoomPhoto> photos, CancellationToken cancellationToken = default)
    {
        // Cùng partition key (roomId) nên ghi song song an toàn và rẻ.
        await Task.WhenAll(photos.Select(p =>
            _container.CreateItemAsync(p, new PartitionKey(p.RoomId), cancellationToken: cancellationToken)));
    }

    public async Task<IReadOnlyList<RoomPhoto>> GetByRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        // Truy vấn trong 1 partition (roomId) - nhanh, tốn ít RU.
        var query = _container
            .GetItemLinqQueryable<RoomPhoto>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(roomId) },
                linqSerializerOptions: LinqOptions)
            .OrderBy(p => p.UploadedAt);

        var results = new List<RoomPhoto>();
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        return results;
    }
}
