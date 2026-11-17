using RoomToRentRadar.Features.RoomDetails.Models;
using RoomToRentRadar.Features.RoomDetails.Services;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomDetailServiceTests
{
    [Test]
    public async Task GetSimilarRoomsAsync_WhenApiResponseContainsNoSimilarRooms_ReturnsEmptyList()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            IsAvailable = false
        }));

        var result = await service.GetSimilarRoomsAsync("room-1");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRoomDetailAsync_WhenApiOmitsLandlordAndRating_UsesUnknownInformationState()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            Title = "Room from API"
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.LandlordName, Is.EqualTo("Chưa có thông tin chủ trọ"));
            Assert.That(result.LandlordEmail, Is.Null);
            Assert.That(result.LandlordAvatar, Is.Null);
            Assert.That(result.Rating, Is.Null);
            Assert.That(result.ReviewCount, Is.Null);
            Assert.That(result.RatingDisplay, Is.EqualTo("Chưa có đánh giá"));
        });
    }

    private sealed class FakeApiClient(Func<string, object?> handler) : IApiClient
    {
        public Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse?)handler(endpoint));
    }
}
