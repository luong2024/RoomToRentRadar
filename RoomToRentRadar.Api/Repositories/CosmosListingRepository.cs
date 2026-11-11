using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Options;

namespace RoomToRentRadar.Api.Repositories;

/// <summary>
/// Repository đọc phòng trọ từ Azure Cosmos DB bằng Cosmos LINQ provider.
/// Phân trang + sắp xếp được dịch sang SQL và chạy trên server Cosmos,
/// mỗi request chỉ kéo về đúng PageSize document.
/// </summary>
public class CosmosListingRepository : IListingRepository
{
    private static readonly CosmosLinqSerializerOptions LinqOptions = new()
    {
        // Khớp với SerializerOptions (camelCase) cấu hình cho CosmosClient trong Program.cs
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    };

    private readonly Container _container;
    private readonly ILogger<CosmosListingRepository> _logger;

    public CosmosListingRepository(CosmosClient client, IOptions<CosmosDbOptions> options, ILogger<CosmosListingRepository> logger)
    {
        var o = options.Value;
        _container = client.GetContainer(o.DatabaseName, o.ListingsContainer);
        _logger = logger;
    }

    public async Task<(IReadOnlyList<Listing> Items, int TotalRecords)> GetPagedAsync(
        ListingQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // IQueryable gốc – CHƯA gửi request nào tới Cosmos.
        IQueryable<Listing> baseQuery = _container.GetItemLinqQueryable<Listing>(linqSerializerOptions: LinqOptions);

        // 1) Đếm tổng: SELECT VALUE COUNT(1) FROM root  (chỉ trả về 1 con số)
        Task<Response<int>> countTask = baseQuery.CountAsync(cancellationToken);

        // 2) Lấy trang: SELECT VALUE root FROM root ORDER BY root.price ASC OFFSET @skip LIMIT @take
        IQueryable<Listing> pageQuery = baseQuery.ApplyQuery(parameters);
        Task<List<Listing>> itemsTask = ExecuteAsync(pageQuery, cancellationToken);

        // Chạy song song 2 truy vấn để giảm độ trễ.
        await Task.WhenAll(countTask, itemsTask);

        var countResponse = await countTask;
        _logger.LogDebug("Count query RU: {RequestCharge}", countResponse.RequestCharge);

        return (await itemsTask, countResponse.Resource);
    }

    private async Task<List<Listing>> ExecuteAsync(IQueryable<Listing> query, CancellationToken cancellationToken)
    {
        var results = new List<Listing>();
        double totalRu = 0;

        // ToFeedIterator() mới thực sự gửi truy vấn (đã dịch sang SQL) tới Cosmos DB.
        using FeedIterator<Listing> iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Listing> page = await iterator.ReadNextAsync(cancellationToken);
            totalRu += page.RequestCharge;
            results.AddRange(page);
        }

        _logger.LogDebug("Page query RU: {RequestCharge}, items: {Count}", totalRu, results.Count);
        return results;
    }
}
