using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public class RoomListingsServiceTests
{
    [Test]
    public async Task GetRoomListingsAsync_WhenApiSucceeds_ReturnsApiResultAndBuildsCorrectQuery()
    {
        // Arrange
        string? capturedEndpoint = null;
        var expectedResponse = new PagedResponse<RoomListItemModel>(
            Items:
            [
                new RoomListItemModel
                {
                    Id = "api-room-1",
                    Title = "Phòng từ API",
                    Price = 1_700_000m,
                    AreaSqm = 22,
                    Address = "Z115 Quyết Thắng"
                }
            ],
            Page: 2,
            PageSize: 6,
            TotalCount: 10);

        var fakeApiClient = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return expectedResponse;
        });

        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);

        // Act
        var result = await service.GetRoomListingsAsync(2, 6, RoomSortOption.PriceAsc);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(capturedEndpoint, Is.EqualTo("api/rooms?page=2&pageSize=6&sortBy=price_asc"));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("api-room-1"));
            Assert.That(result.TotalCount, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenApiUnreachableAndDebugFallbackEnabled_ReturnsSortedMockPage()
    {
        // Arrange
        var fakeApiClient = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: true);

        // Act
        var page1 = await service.GetRoomListingsAsync(1, 5, RoomSortOption.PriceAsc);
        var page2 = await service.GetRoomListingsAsync(2, 5, RoomSortOption.PriceAsc);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(page1.Items, Has.Count.EqualTo(5));
            Assert.That(page2.Items, Has.Count.EqualTo(5));
            Assert.That(page1.TotalCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(page1.Items.Select(r => r.Price), Is.Ordered.Ascending);
            Assert.That(page2.Items[0].Price, Is.GreaterThanOrEqualTo(page1.Items[^1].Price));
        });
    }

    [Test]
    public void GetRoomListingsAsync_WhenApiUnreachableAndDebugFallbackDisabled_PropagatesException()
    {
        // Arrange
        var fakeApiClient = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetRoomListingsAsync(1, 6, RoomSortOption.Newest));
    }

    private sealed class FakeApiClient(Func<string, object?> handler) : IApiClient
    {
        public Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
        {
            var result = handler(endpoint);
            return Task.FromResult((TResponse?)result);
        }
    }
}
