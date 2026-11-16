using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public class RoomListingsServiceTests
{
    [Test]
    public async Task GetRoomListingsAsync_UsesVerifiedIctuEndpointAndMapsApiItems()
    {
        string? capturedEndpoint = null;
        var client = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return new PagedResponse<RoomListingApiItem>(
                [new RoomListingApiItem
                {
                    Id = "room-1", Title = "Phòng trọ từ backend", MonthlyRent = 1_700_000m,
                    AreaSquareMeters = 22, DistanceToCampusKm = 0.4, Address = "Đường Z115", IsVerified = true
                }], 2, 6, 10);
        });

        var result = await new RoomListingsService(client).GetRoomListingsAsync(2, 6, RoomSortOption.PriceAsc);

        Assert.Multiple(() =>
        {
            Assert.That(capturedEndpoint, Is.EqualTo("api/v1/rooms?campusId=campus-ictu&page=2&pageSize=6&sortBy=price&sortOrder=asc"));
            Assert.That(result.TotalCount, Is.EqualTo(10));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].FormattedPrice, Is.EqualTo("₫1,700,000/month"));
            Assert.That(result.Items[0].IsVerified, Is.True);
        });
    }

    [Test]
    public async Task GetRoomListingsAsync_PriceDescendingUsesBackendSortContract()
    {
        string? capturedEndpoint = null;
        var client = new FakeApiClient(endpoint =>
        {
            capturedEndpoint = endpoint;
            return new PagedResponse<RoomListingApiItem>([], 1, 6, 0);
        });

        await new RoomListingsService(client).GetRoomListingsAsync(1, 6, RoomSortOption.PriceDesc);

        Assert.That(capturedEndpoint, Does.EndWith("sortBy=price&sortOrder=desc"));
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenBackendIsUnavailableInDebug_ReturnsFilterableFallbackPage()
    {
        var client = new FakeApiClient(_ => throw new HttpRequestException("Backend offline"));

        var result = await new RoomListingsService(client, enableDebugFallback: true)
            .GetRoomListingsAsync(1, 6, RoomSortOption.PriceAsc, search: "Z115");

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Not.Empty);
            Assert.That(result.Items.All(item => item.Address.Contains("Z115", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(result.Items.Select(item => item.Price), Is.Ordered.Ascending);
        });
    }

    [Test]
    public async Task GetRoomListingsAsync_WhenSearchIsProvided_FiltersReturnedBackendItems()
    {
        var client = new FakeApiClient(_ => new PagedResponse<RoomListingApiItem>(
            [
                new RoomListingApiItem { Id = "1", Title = "Phòng có điều hòa", Address = "Z115" },
                new RoomListingApiItem { Id = "2", Title = "Studio", Address = "Quyết Thắng" }
            ], 1, 6, 2));

        var result = await new RoomListingsService(client).GetRoomListingsAsync(1, 6, search: " điều hòa ");

        Assert.That(result.Items.Select(item => item.Id), Is.EqualTo(new[] { "1" }));
    }

    private sealed class FakeApiClient(Func<string, object?> handler) : IApiClient
    {
        public Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse?)handler(endpoint));
    }
}
