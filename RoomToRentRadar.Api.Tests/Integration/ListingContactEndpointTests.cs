using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingContactEndpointTests
{
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
        await TestListingsSeed.SeedAsync(dbContext);
        await TestUsersSeed.SeedAsync(dbContext);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetContact_Anonymous_ReturnsUnauthorizedProblem()
    {
        var response = await _client.GetAsync($"/api/v1/listings/{await SeededIdAsync(0)}/contact");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("unauthorized"));
    }

    [Test]
    public async Task GetContact_Authenticated_ReturnsLandlordContact()
    {
        var id = await SeededIdAsync(0);
        var contact = await GetContactAsync(id, await AccessTokenAsync());

        Assert.Multiple(() =>
        {
            Assert.That(contact.LandlordName, Is.EqualTo("Landlord 0"));
            Assert.That(contact.LandlordPhone, Is.EqualTo("+84 900 000 000"));
        });
    }

    [Test]
    public async Task GetContact_UnknownId_ReturnsNotFound()
    {
        var response = await SendContactAsync(Guid.NewGuid(), await AccessTokenAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("listing_not_found"));
    }

    [TestCase(20)]
    [TestCase(22)]
    [TestCase(24)]
    public async Task GetContact_NonPublishedListing_ReturnsGone(int listingIndex)
    {
        var response = await SendContactAsync(
            TestListingsSeed.IdFor(listingIndex),
            await AccessTokenAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("listing_unavailable"));
    }

    private async Task<HttpResponseMessage> SendContactAsync(Guid id, string accessToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/listings/{id}/contact");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<ListingContactDto> GetContactAsync(Guid id, string accessToken)
    {
        var response = await SendContactAsync(id, accessToken);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var contact = await response.Content.ReadFromJsonAsync<ListingContactDto>(ApiJson.Options);
        Assert.That(contact, Is.Not.Null);
        return contact!;
    }

    private async Task<string> AccessTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(TestUsersSeed.Email, TestUsersSeed.Password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }

    private async Task<Guid> SeededIdAsync(int listingIndex)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Listings
            .Where(x => x.Title == $"Test listing {listingIndex}")
            .Select(x => x.Id)
            .SingleAsync();
    }
}
