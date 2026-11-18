using RoomToRentRadar.Features.RoomDetails.Models;
using RoomToRentRadar.Features.RoomDetails.Services;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomDetailServiceTests
{
    [Test]
    public async Task GetRoomDetailAsync_WhenAvailabilityIsMissing_FailsClosedAsUnavailable()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto { Id = "room-1" }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo(ListingStatus.Unavailable));
            Assert.That(result.IsAvailable, Is.False);
        });
    }

    [Test]
    public async Task GetRoomDetailAsync_WhenStatusIsRented_PrefersStatusOverAvailabilityFlag()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            Status = "Rented",
            IsAvailable = true
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo(ListingStatus.Rented));
            Assert.That(result.IsAvailable, Is.False);
        });
    }

    [Test]
    public async Task GetRoomDetailAsync_WhenStatusIsOccupied_MapsToRented()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            Status = "Occupied",
            IsAvailable = false
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo(ListingStatus.Rented));
            Assert.That(result.IsAvailable, Is.False);
        });
    }

    [Test]
    public async Task GetRoomDetailAsync_WhenStatusIsAvailable_PrefersStatusOverAvailabilityFlag()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            Status = "Available",
            IsAvailable = false
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(result.IsAvailable, Is.True);
        });
    }

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
            Assert.That(result!.LandlordName, Is.EqualTo("Landlord information unavailable"));
            Assert.That(result.LandlordEmail, Is.Null);
            Assert.That(result.LandlordAvatar, Is.Null);
            Assert.That(result.Rating, Is.Null);
            Assert.That(result.ReviewCount, Is.Null);
            Assert.That(result.RatingDisplay, Is.EqualTo("No ratings yet"));
        });
    }

    [Test]
    public async Task GetRoomDetailAsync_MapsOnlyFinancialAndRoomFactsProvidedByApi()
    {
        var updatedAt = new DateTime(2026, 11, 17, 9, 30, 0, DateTimeKind.Utc);
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            Price = 2_000_000m,
            Deposit = 0m,
            Bedrooms = 2,
            Bathrooms = 1,
            LastUpdatedDate = updatedAt,
            ImageUrls = ["https://cdn.example.test/room-1.jpg"]
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Deposit, Is.Zero);
            Assert.That(result.TotalPackage, Is.EqualTo(2_000_000m));
            Assert.That(result.Bedrooms, Is.EqualTo(2));
            Assert.That(result.Bathrooms, Is.EqualTo(1));
            Assert.That(result.LastUpdated, Is.EqualTo(updatedAt));
            Assert.That(result.ImageUrls, Is.EqualTo(new[] { "https://cdn.example.test/room-1.jpg" }));
        });
    }

    [Test]
    public async Task GetRoomDetailAsync_WhenApiOmitsOptionalFacts_DoesNotInventValues()
    {
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            ThumbnailUrl = "https://cdn.example.test/thumbnail.jpg"
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Deposit, Is.Null);
            Assert.That(result.FormattedDeposit, Is.EqualTo("Not provided"));
            Assert.That(result.TotalPackage, Is.Null);
            Assert.That(result.FormattedTotalPackage, Is.EqualTo("Not provided"));
            Assert.That(result.Bedrooms, Is.Null);
            Assert.That(result.Bathrooms, Is.Null);
            Assert.That(result.LastUpdated, Is.Null);
            Assert.That(result.HasLastUpdated, Is.False);
            Assert.That(result.ImageUrls, Is.Empty);
            Assert.That(result.HasImages, Is.False);
        });
    }

    [Test]
    public async Task GetRoomDetailAsync_WhenApiProvidesSimilarRoomThumbnail_MapsThumbnailUrl()
    {
        const string thumbnailUrl = "https://cdn.example.test/similar-room.jpg";
        var service = new RoomDetailService(new FakeApiClient(_ => new RoomDetailApiDto
        {
            Id = "room-1",
            SimilarRooms =
            [
                new SimilarRoomApiDto
                {
                    Id = "room-2",
                    ThumbnailUrl = thumbnailUrl
                }
            ]
        }));

        var result = await service.GetRoomDetailAsync("room-1");

        Assert.That(result!.SimilarRooms, Has.Count.EqualTo(1));
        Assert.That(result.SimilarRooms[0].ThumbnailUrl, Is.EqualTo(thumbnailUrl));
    }

    private sealed class FakeApiClient(Func<string, object?> handler) : IApiClient
    {
        public Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse?)handler(endpoint));
    }
}
