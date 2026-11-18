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
        var expectedResponse = new PagedResponse<RoomListingApiDto>(
            Items:
            [
                new RoomListingApiDto
                {
                    Id = "api-room-1",
                    Title = "Phòng từ API",
                    MonthlyRent = 1_700_000m,
                    AreaSquareMeters = 22,
                    Address = "Z115 Quyết Thắng",
                    DistanceToCampusKm = 0.5,
                    CampusId = "campus-ictu",
                    IsVerified = true
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
            Assert.That(capturedEndpoint, Is.EqualTo("api/v1/rooms?campusId=campus-ictu&page=2&pageSize=6&sortBy=price&sortOrder=asc"));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("api-room-1"));
            Assert.That(result.Items[0].Price, Is.EqualTo(1_700_000m));
            Assert.That(result.Items[0].AreaSqm, Is.EqualTo(22));
            Assert.That(result.Items[0].IsVerified, Is.True);
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

    [Test]
    public async Task GetRoomByIdAsync_WhenApiUnreachableAndDebugFallbackEnabled_ReturnsMatchingMockRoom()
    {
        // Arrange
        var fakeApiClient = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: true);

        // Act
        var room = await service.GetRoomByIdAsync("room-ictu-001");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(room, Is.Not.Null);
            Assert.That(room!.Id, Is.EqualTo("room-ictu-001"));
            Assert.That(room.IsVerified, Is.False);
        });
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenDebugFallbackUsed_AllMockRoomsAreUnverified()
    {
        // Arrange
        var fakeApiClient = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: true);

        // Act
        var page = await service.GetRoomListingsAsync(1, 50, RoomSortOption.Newest);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Is.Not.Empty);
            Assert.That(page.Items.All(r => !r.IsVerified), Is.True);
        });
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenSearchProvided_AppendsEscapedSearchQuery()
    {
        // Arrange
        string? capturedEndpoint = null;
        var fakeApiClient = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return new PagedResponse<RoomListingApiDto>([], 1, 6, 0);
        });
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);

        // Act
        await service.GetRoomListingsAsync(1, 6, RoomSortOption.Newest, search: "  điều hòa & gác  ");

        // Assert
        Assert.That(capturedEndpoint, Is.EqualTo(
            "api/v1/rooms?campusId=campus-ictu&page=1&pageSize=6&sortBy=distance&sortOrder=asc&search=" + Uri.EscapeDataString("điều hòa & gác")));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task GetRoomListingsAsync_WhenSearchEmpty_OmitsSearchQuery(string? search)
    {
        // Arrange
        string? capturedEndpoint = null;
        var fakeApiClient = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return new PagedResponse<RoomListingApiDto>([], 1, 6, 0);
        });
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);

        // Act
        await service.GetRoomListingsAsync(1, 6, RoomSortOption.Newest, search);

        // Assert
        Assert.That(capturedEndpoint, Is.EqualTo("api/v1/rooms?campusId=campus-ictu&page=1&pageSize=6&sortBy=distance&sortOrder=asc"));
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenFallbackSearch_FiltersWholeCatalogBeforePaging()
    {
        // Arrange
        var fakeApiClient = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: true);

        // Act: page size 1 forces matches beyond the first page to be reachable only via paging
        var page1 = await service.GetRoomListingsAsync(1, 1, RoomSortOption.Newest, "Điều hòa");
        var page2 = await service.GetRoomListingsAsync(2, 1, RoomSortOption.Newest, "Điều hòa");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(page1.TotalCount, Is.GreaterThan(1));
            Assert.That(page1.Items, Has.Count.EqualTo(1));
            Assert.That(page2.Items, Has.Count.EqualTo(1));
            Assert.That(page2.Items[0].Id, Is.Not.EqualTo(page1.Items[0].Id));
            Assert.That(page1.Items.Concat(page2.Items).All(r =>
                r.Title.Contains("Điều hòa", StringComparison.OrdinalIgnoreCase) ||
                r.Address.Contains("Điều hòa", StringComparison.OrdinalIgnoreCase) ||
                r.Amenities.Any(a => a.Contains("Điều hòa", StringComparison.OrdinalIgnoreCase))), Is.True);
        });
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenFilterProvided_AppendsFilterQueryParametersToEndpoint()
    {
        // Arrange
        string? capturedEndpoint = null;
        var fakeApiClient = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return new PagedResponse<RoomListingApiDto>([], 1, 6, 0);
        });
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);
        var filter = new RoomFilterCriteria
        {
            MinPrice = 1_000_000m,
            MaxPrice = 2_000_000m,
            RoomType = "private",
            Amenities = ["aircon", "wifi"]
        };

        // Act
        await service.GetRoomListingsAsync(1, 6, RoomSortOption.PriceAsc, search: "z115", filter: filter);

        // Assert
        Assert.That(
            capturedEndpoint,
            Is.EqualTo("api/v1/rooms?campusId=campus-ictu&page=1&pageSize=6&sortBy=price&sortOrder=asc&search=z115&minPrice=1000000&maxPrice=2000000&roomType=private&amenities=aircon,wifi"));
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenFallbackFilter_FiltersBeforePagingAndUpdatesTotalCount()
    {
        // Arrange
        var fakeApiClient = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: true);
        var filter = new RoomFilterCriteria
        {
            MinPrice = 1_000_000m,
            MaxPrice = 2_000_000m,
            RoomType = "private",
            Amenities = ["aircon"]
        };

        // Act
        var page1 = await service.GetRoomListingsAsync(1, 2, RoomSortOption.PriceAsc, filter: filter);
        var page2 = await service.GetRoomListingsAsync(2, 2, RoomSortOption.PriceAsc, filter: filter);

        // Assert
        var allFilteredItems = page1.Items.Concat(page2.Items).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(page1.TotalCount, Is.EqualTo(allFilteredItems.Count));
            Assert.That(page1.TotalCount, Is.GreaterThan(2));
            Assert.That(allFilteredItems.All(r =>
                r.Price is >= 1_000_000m and <= 2_000_000m &&
                string.Equals(r.RoomType, "private", StringComparison.OrdinalIgnoreCase) &&
                r.Amenities.Any(a => a.Contains("Điều hòa", StringComparison.OrdinalIgnoreCase))), Is.True);
        });
    }

    [TestCase(RoomSortOption.Newest, "distance", "asc")]
    [TestCase(RoomSortOption.DistanceAsc, "distance", "asc")]
    [TestCase(RoomSortOption.PriceAsc, "price", "asc")]
    [TestCase(RoomSortOption.PriceDesc, "price", "desc")]
    public async Task GetRoomListingsAsync_WithSortOption_MapsToExpectedSortByAndSortOrder(
        RoomSortOption sortOption,
        string expectedSortBy,
        string expectedSortOrder)
    {
        // Arrange
        string? capturedEndpoint = null;
        var fakeApiClient = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return new PagedResponse<RoomListingApiDto>([], 1, 6, 0);
        });
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);

        // Act
        await service.GetRoomListingsAsync(1, 6, sortOption);

        // Assert
        Assert.That(
            capturedEndpoint,
            Is.EqualTo($"api/v1/rooms?campusId=campus-ictu&page=1&pageSize=6&sortBy={expectedSortBy}&sortOrder={expectedSortOrder}"));
    }

    [Test]
    public async Task GetRoomByIdAsync_WhenApiSucceeds_MapsDtoToModel()
    {
        // Arrange
        string? capturedEndpoint = null;
        var dto = new RoomListingApiDto
        {
            Id = "room-v1-99",
            Title = "Phòng trọ sinh viên chất lượng cao",
            MonthlyRent = 2_100_000m,
            AreaSquareMeters = 25,
            DistanceToCampusKm = 0.4,
            Address = "Đường Z115, Quyết Thắng",
            ThumbnailUrl = "https://example.com/room99.jpg",
            IsVerified = true,
            Amenities = ["Điều hòa", "Nóng lạnh"]
        };

        var fakeApiClient = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return dto;
        });
        var service = new RoomListingsService(fakeApiClient, enableDebugFallback: false);

        // Act
        var result = await service.GetRoomByIdAsync("room-v1-99");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(capturedEndpoint, Is.EqualTo("api/rooms/room-v1-99"));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Id, Is.EqualTo("room-v1-99"));
            Assert.That(result.Price, Is.EqualTo(2_100_000m));
            Assert.That(result.AreaSqm, Is.EqualTo(25));
            Assert.That(result.DistanceToCampusKm, Is.EqualTo(0.4));
            Assert.That(result.IsVerified, Is.True);
            Assert.That(result.Amenities, Is.EqualTo(new[] { "Điều hòa", "Nóng lạnh" }));
        });
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
