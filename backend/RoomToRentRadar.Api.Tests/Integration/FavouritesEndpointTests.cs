using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class FavouritesEndpointTests
{
    // Index 0 cannot be addressed: ListingsApiFactory lets EF generate a key for
    // Guid.Empty, so a fixed-id listing must come from index 1 or higher.
    private static readonly Guid ManagedListingId = TestListingsSeed.IdFor(2);
    private static readonly Guid PhotoLessListingId = TestListingsSeed.IdFor(1);

    private const string SecondEmail = "second@example.test";
    private static readonly Guid SecondUserId = new("aaaaaaaa-0000-0000-0000-000000000002");

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _factory.ResetDataAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await TestUsersSeed.SeedAsync(dbContext);
        _userId = user.Id;
        await TestListingsSeed.SeedAsync(dbContext);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Get_Anonymous_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/favourites");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task AddAndRemove_Anonymous_ReturnUnauthorized()
    {
        var add = await SendAsync(HttpMethod.Put, ManagedListingId, accessToken: null);
        var remove = await SendAsync(HttpMethod.Delete, ManagedListingId, accessToken: null);

        await AssertProblemAsync(add, HttpStatusCode.Unauthorized, "unauthorized");
        await AssertProblemAsync(remove, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Get_WithoutFavourites_ReturnsEmptyList()
    {
        var favourites = await GetFavouritesAsync(await AccessTokenAsync());

        Assert.That(favourites, Is.Empty);
    }

    [Test]
    public async Task Add_NewFavourite_ReturnsNoContentAndAppearsInList()
    {
        var token = await AccessTokenAsync();

        var response = await SendAsync(HttpMethod.Put, ManagedListingId, token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var favourites = await GetFavouritesAsync(token);
        Assert.That(favourites, Has.Count.EqualTo(1));
        var favourite = favourites[0];
        Assert.Multiple(() =>
        {
            Assert.That(favourite.ListingId, Is.EqualTo(ManagedListingId));
            Assert.That(favourite.Title, Is.EqualTo("Test listing 2"));
            Assert.That(favourite.Price, Is.EqualTo(7_000_000L));
            Assert.That(favourite.AreaM2, Is.EqualTo(40d));
            Assert.That(favourite.Bedrooms, Is.EqualTo(2));
            Assert.That(favourite.Address, Is.EqualTo("Test address 2, District 2"));
            Assert.That(favourite.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(favourite.CoverPhotoUrl, Is.EqualTo("/media/test/thumbs/photo-1.jpg"));
            Assert.That(favourite.FavouritedAt, Is.Not.EqualTo(default(DateTime)));
        });
    }

    [Test]
    public async Task Add_ListingWithoutPhotos_ReturnsNullCover()
    {
        var token = await AccessTokenAsync();

        var response = await SendAsync(HttpMethod.Put, PhotoLessListingId, token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var favourites = await GetFavouritesAsync(token);
        Assert.That(favourites[0].CoverPhotoUrl, Is.Null);
    }

    [Test]
    public async Task Add_UnknownListing_ReturnsListingNotFound()
    {
        var token = await AccessTokenAsync();

        var response = await SendAsync(HttpMethod.Put, Guid.NewGuid(), token);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "listing_not_found");
        Assert.That(await GetFavouritesAsync(token), Is.Empty);
    }

    [Test]
    public async Task Add_Twice_IsIdempotent()
    {
        var token = await AccessTokenAsync();

        var first = await SendAsync(HttpMethod.Put, ManagedListingId, token);
        var second = await SendAsync(HttpMethod.Put, ManagedListingId, token);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
        Assert.That(await GetFavouritesAsync(token), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Remove_Favourite_ReturnsNoContentAndIsIdempotent()
    {
        var token = await AccessTokenAsync();
        Assert.That(
            (await SendAsync(HttpMethod.Put, ManagedListingId, token)).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var remove = await SendAsync(HttpMethod.Delete, ManagedListingId, token);
        var removeAgain = await SendAsync(HttpMethod.Delete, ManagedListingId, token);

        Assert.Multiple(() =>
        {
            Assert.That(remove.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(removeAgain.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
        Assert.That(await GetFavouritesAsync(token), Is.Empty);
    }

    [Test]
    public async Task Favourites_AreIsolatedBetweenUsers()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await TestUsersSeed.SeedAdditionalAsync(
                scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                SecondUserId,
                SecondEmail);
        }

        var ownerToken = await AccessTokenAsync();
        var secondToken = await AccessTokenAsync(SecondEmail);
        Assert.That(
            (await SendAsync(HttpMethod.Put, ManagedListingId, ownerToken)).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var secondFavourites = await GetFavouritesAsync(secondToken);
        Assert.That(secondFavourites, Is.Empty);

        Assert.That(
            (await SendAsync(HttpMethod.Delete, ManagedListingId, secondToken)).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await GetFavouritesAsync(ownerToken), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Get_ReflectsCurrentPriceAndAvailability()
    {
        var token = await AccessTokenAsync();
        Assert.That(
            (await SendAsync(HttpMethod.Put, ManagedListingId, token)).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var listing = await dbContext.Listings.SingleAsync(
                candidate => candidate.Id == ManagedListingId);
            listing.Price = 9_900_000L;
            listing.Status = ListingStatus.Hidden;
            await dbContext.SaveChangesAsync();
        }

        var favourites = await GetFavouritesAsync(token);

        Assert.Multiple(() =>
        {
            Assert.That(favourites[0].Price, Is.EqualTo(9_900_000L));
            Assert.That(favourites[0].Status, Is.EqualTo(ListingStatus.Hidden));
        });
    }

    [Test]
    public async Task Get_ReturnsMostRecentlyFavouritedFirst()
    {
        var olderListingId = ManagedListingId;
        var newerListingId = TestListingsSeed.IdFor(3);
        var now = DateTime.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.ListingFavourites.Add(new ListingFavourite
            {
                UserId = _userId,
                ListingId = olderListingId,
                CreatedAt = now.AddMinutes(-5)
            });
            dbContext.ListingFavourites.Add(new ListingFavourite
            {
                UserId = _userId,
                ListingId = newerListingId,
                CreatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }

        var favourites = await GetFavouritesAsync(await AccessTokenAsync());

        Assert.That(
            favourites.Select(favourite => favourite.ListingId),
            Is.EqualTo(new[] { newerListingId, olderListingId }));
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Guid listingId,
        string? accessToken)
    {
        var message = new HttpRequestMessage(method, $"/api/v1/favourites/{listingId}");
        if (accessToken is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(message);
    }

    private async Task<List<FavouriteRoomDto>> GetFavouritesAsync(string accessToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/v1/favourites");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(message);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<List<FavouriteRoomDto>>(ApiJson.Options))!;
    }

    private async Task<string> AccessTokenAsync(
        string email = TestUsersSeed.Email,
        string password = TestUsersSeed.Password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        return login!.AccessToken;
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(status));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo(code));
    }
}
