using Moq;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Services.RoomListings;

namespace RoomToRentRadarUnitTests.Features.RoomListings;

[TestFixture]
public sealed class FavouritesServiceTests
{
    [Test]
    public async Task GetMyFavouritesAsync_UsesExistingApiRouteAndMapsRoom()
    {
        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(client => client.GetAsync<FavouritesService.FavouriteListResponse>("api/v1/favourites", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavouritesService.FavouriteListResponse
            {
                Items = [new FavouritesService.FavouriteResponse
                {
                    RoomId = "room-1", Title = "Room 1", Price = 1_500_000, Area = 20, Address = "Campus road", IsAvailable = true
                }]
            });
        var service = new FavouritesService(apiClient.Object);

        var rooms = await service.GetMyFavouritesAsync();

        Assert.That(rooms.Single().IsFavorite, Is.True);
        Assert.That(rooms.Single().Id, Is.EqualTo("room-1"));
        apiClient.Verify(client => client.GetAsync<FavouritesService.FavouriteListResponse>("api/v1/favourites", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AddFavouriteAsync_PostsRoomIdToExistingApiRoute()
    {
        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(client => client.PostAsync<FavouritesService.AddFavouriteRequest, FavouritesService.FavouriteResponse>(
                "api/v1/favourites", It.Is<FavouritesService.AddFavouriteRequest>(request => request.RoomId == "room-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavouritesService.FavouriteResponse { RoomId = "room-1" });

        await new FavouritesService(apiClient.Object).AddFavouriteAsync("room-1");

        apiClient.Verify(client => client.PostAsync<FavouritesService.AddFavouriteRequest, FavouritesService.FavouriteResponse>(
            "api/v1/favourites", It.Is<FavouritesService.AddFavouriteRequest>(request => request.RoomId == "room-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveFavouriteAsync_DeletesEscapedRoomIdFromExistingApiRoute()
    {
        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(client => client.DeleteAsync("api/v1/favourites/room%20%2F%201", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await new FavouritesService(apiClient.Object).RemoveFavouriteAsync("room / 1");

        apiClient.Verify(client => client.DeleteAsync("api/v1/favourites/room%20%2F%201", It.IsAny<CancellationToken>()), Times.Once);
    }
}
