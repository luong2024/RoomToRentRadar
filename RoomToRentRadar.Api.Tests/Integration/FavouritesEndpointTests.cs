using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class FavouritesEndpointTests
{
    private static readonly Guid OtherUserId = new("bbbbbbbb-0000-0000-0000-000000000002");
    private const string OtherUserEmail = "other@example.test";

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

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
        await TestUsersSeed.SeedAsync(dbContext);
        await TestListingsSeed.SeedAsync(dbContext);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Get_Anonymous_ReturnsUnauthorizedProblem()
    {
        var response = await _client.GetAsync("/api/v1/favourites");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Put_Anonymous_ReturnsUnauthorizedProblem()
    {
        var response = await _client.PutAsync(
            $"/api/v1/favourites/{TestListingsSeed.IdFor(0)}",
            content: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Put_PublishedListing_AddsFavourite()
    {
        var token = await AccessTokenAsync();
        var listingId = await ListingIdAsync(0);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var favourites = await GetFavouritesAsync(token);
        Assert.That(favourites, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(favourites[0].ListingId, Is.EqualTo(listingId));
            Assert.That(favourites[0].Title, Is.EqualTo("Test listing 0"));
            Assert.That(favourites[0].Price, Is.EqualTo(5_000_000));
            Assert.That(favourites[0].AreaM2, Is.EqualTo(30));
            Assert.That(favourites[0].Bedrooms, Is.EqualTo(0));
            Assert.That(favourites[0].Address, Is.EqualTo("Test address 0, District 0"));
            Assert.That(favourites[0].CoverPhotoUrl, Is.EqualTo("/media/test/thumbs/cover-special.jpg"));
            Assert.That(favourites[0].IsAvailable, Is.True);
        });
    }

    [Test]
    public async Task Put_Twice_KeepsSingleFavouriteAndOriginalTimestamp()
    {
        var token = await AccessTokenAsync();
        var listingId = await ListingIdAsync(0);

        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);
        var first = (await GetFavouritesAsync(token)).Single();
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);
        var second = (await GetFavouritesAsync(token)).Single();
        var rowCount = await CountRowsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(second.FavouritedAt, Is.EqualTo(first.FavouritedAt));
            Assert.That(rowCount, Is.EqualTo(1));
        });
    }

    [TestCase(20)]
    [TestCase(22)]
    [TestCase(24)]
    public async Task Put_ListingThatIsNotPublished_ReturnsListingNotFound(int listingIndex)
    {
        var token = await AccessTokenAsync();

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/favourites/{await ListingIdAsync(listingIndex)}",
            token);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "listing_not_found");
    }

    [Test]
    public async Task Put_UnknownListing_ReturnsListingNotFound()
    {
        var token = await AccessTokenAsync();

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/favourites/{Guid.NewGuid()}",
            token);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "listing_not_found");
    }

    [Test]
    public async Task Get_ReflectsCurrentPriceAfterListingUpdate()
    {
        var token = await AccessTokenAsync();
        var listingId = await ListingIdAsync(0);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Listings
                .Where(listing => listing.Id == listingId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(listing => listing.Price, 9_900_000L));
        }

        var favourite = (await GetFavouritesAsync(token)).Single();
        Assert.That(favourite.Price, Is.EqualTo(9_900_000));
    }

    [Test]
    public async Task Get_ListingBecomesHidden_StaysListedAsUnavailable()
    {
        var token = await AccessTokenAsync();
        var listingId = await ListingIdAsync(0);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);

        await SetListingStatusAsync(listingId, ListingStatus.Hidden);

        var favourite = (await GetFavouritesAsync(token)).Single();
        Assert.That(favourite.IsAvailable, Is.False);
    }

    [Test]
    public async Task Get_ListingReentersReview_IsExcluded()
    {
        var token = await AccessTokenAsync();
        var listingId = await ListingIdAsync(0);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);

        await SetListingStatusAsync(listingId, ListingStatus.PendingReview);

        Assert.That(await GetFavouritesAsync(token), Is.Empty);
    }

    [Test]
    public async Task Delete_RemovesFavouriteAndIsIdempotent()
    {
        var token = await AccessTokenAsync();
        var listingId = await ListingIdAsync(0);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", token);

        var first = await SendAsync(HttpMethod.Delete, $"/api/v1/favourites/{listingId}", token);
        var second = await SendAsync(HttpMethod.Delete, $"/api/v1/favourites/{listingId}", token);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
        Assert.That(await GetFavouritesAsync(token), Is.Empty);
    }

    [Test]
    public async Task Favourites_AreScopedToTheCurrentUser()
    {
        var ownerToken = await AccessTokenAsync();
        var otherToken = await AccessTokenAsync(OtherUserEmail);
        var listingId = await ListingIdAsync(0);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{listingId}", ownerToken);

        var otherFavourites = await GetFavouritesAsync(otherToken);
        var otherDelete = await SendAsync(HttpMethod.Delete, $"/api/v1/favourites/{listingId}", otherToken);
        var ownerFavourites = await GetFavouritesAsync(ownerToken);

        Assert.Multiple(() =>
        {
            Assert.That(otherFavourites, Is.Empty);
            Assert.That(otherDelete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(ownerFavourites, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Get_ReturnsNewestFavouriteFirst()
    {
        var token = await AccessTokenAsync();
        var olderListingId = await ListingIdAsync(0);
        var newerListingId = await ListingIdAsync(1);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{olderListingId}", token);
        await SendAsync(HttpMethod.Put, $"/api/v1/favourites/{newerListingId}", token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
            await dbContext.Favourites
                .Where(favourite => favourite.ListingId == olderListingId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(favourite => favourite.CreatedAt, baseDate));
            await dbContext.Favourites
                .Where(favourite => favourite.ListingId == newerListingId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(favourite => favourite.CreatedAt, baseDate.AddHours(1)));
        }

        var favourites = await GetFavouritesAsync(token);

        Assert.Multiple(() =>
        {
            Assert.That(favourites, Has.Count.EqualTo(2));
            Assert.That(favourites[0].ListingId, Is.EqualTo(newerListingId));
            Assert.That(favourites[1].ListingId, Is.EqualTo(olderListingId));
        });
    }

    private async Task<IReadOnlyList<FavouriteListingDto>> GetFavouritesAsync(string token)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/favourites", token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<List<FavouriteListingDto>>(ApiJson.Options)
            ?? throw new InvalidOperationException("The favourites endpoint returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<int> CountRowsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Favourites.CountAsync();
    }

    private async Task<Guid> ListingIdAsync(int index)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Title == $"Test listing {index}")
            .Select(listing => listing.Id)
            .SingleAsync();
    }

    private async Task SetListingStatusAsync(Guid listingId, ListingStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Listings
            .Where(listing => listing.Id == listingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(listing => listing.Status, status));
    }

    private async Task<string> AccessTokenAsync(string? email = null)
    {
        if (email is not null)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TestUsersSeed.SeedAdditionalAsync(dbContext, OtherUserId, email);
        }

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email ?? TestUsersSeed.Email, TestUsersSeed.Password),
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
