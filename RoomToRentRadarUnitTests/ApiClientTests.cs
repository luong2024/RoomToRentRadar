using System.Net;
using System.Text;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ApiClientTests
{
    [Test]
    public async Task GetListingsAsync_WithFilters_BuildsSnakeCaseQueryAndParsesSearchResult()
    {
        var handler = new RecordingHandler("""
            {
              "data": [],
              "pagination": { "limit": 12, "next_cursor": "next-page", "has_more": true },
              "totalCount": 42,
              "totalCountCapped": false,
              "activeFilters": { "roomType": "studio", "maxDistanceKm": 5 }
            }
            """);
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var result = await client.GetListingsAsync(
            12,
            "cursor value",
            new ActiveListingFiltersDto(
                1_500_000,
                3_000_000,
                20,
                40,
                null,
                RoomType.Studio,
                10.7721,
                106.6579,
                5));

        var query = handler.RequestUri!.Query;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("limit=12"));
            Assert.That(query, Does.Contain("cursor=cursor%20value"));
            Assert.That(query, Does.Contain("min_price=1500000"));
            Assert.That(query, Does.Contain("max_price=3000000"));
            Assert.That(query, Does.Contain("min_area_m2=20"));
            Assert.That(query, Does.Contain("max_area_m2=40"));
            Assert.That(query, Does.Contain("room_type=studio"));
            Assert.That(query, Does.Contain("campus_lat=10.7721"));
            Assert.That(query, Does.Contain("campus_lng=106.6579"));
            Assert.That(query, Does.Contain("max_distance_km=5"));
            Assert.That(result.TotalCount, Is.EqualTo(42));
            Assert.That(result.Pagination.NextCursor, Is.EqualTo("next-page"));
            Assert.That(result.ActiveFilters?.RoomType, Is.EqualTo(RoomType.Studio));
        });
    }

    [Test]
    public async Task GetListingsAsync_WithoutFilters_OmitsFilterParameters()
    {
        var handler = new RecordingHandler("""
            {
              "data": [],
              "pagination": { "limit": 12, "next_cursor": null, "has_more": false },
              "totalCount": 0,
              "totalCountCapped": false,
              "activeFilters": null
            }
            """);
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        await client.GetListingsAsync(12);

        var query = handler.RequestUri!.Query;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Not.Contain("min_price"));
            Assert.That(query, Does.Not.Contain("min_area_m2"));
            Assert.That(query, Does.Not.Contain("room_type"));
            Assert.That(query, Does.Not.Contain("campus_lat"));
            Assert.That(query, Does.Not.Contain("max_distance_km"));
        });
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
