using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingDetailEndpointTests
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
        _factory.QueryCounter.Reset();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetDetail_PublishedListing_ReturnsDescriptionAmenitiesAndRules()
    {
        var id = await SeededIdAsync(0);
        var detail = await GetDetailAsync(id);

        Assert.Multiple(() =>
        {
            Assert.That(detail.Id, Is.EqualTo(id));
            Assert.That(detail.Title, Is.EqualTo("Test listing 0"));
            Assert.That(detail.Description, Is.EqualTo("Test listing 0 description"));
            Assert.That(detail.Price, Is.EqualTo(5_000_000L));
            Assert.That(detail.Deposit, Is.EqualTo(5_000_000L));
            Assert.That(detail.RoomType, Is.EqualTo(RoomType.Apartment));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(detail.CreatedAt, Is.EqualTo(DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc)));
            Assert.That(detail.UpdatedAt, Is.EqualTo(DateTime.SpecifyKind(new DateTime(2026, 1, 1, 0, 5, 0), DateTimeKind.Utc)));
            Assert.That(detail.Amenities, Is.EqualTo(new[] { AmenityCode.Wifi, AmenityCode.Parking }));
            Assert.That(detail.Rules, Is.EqualTo(new[] { "No smoking", "No pets" }));
            Assert.That(detail.LandlordName, Is.EqualTo("Landlord 0"));
        });
    }

    [Test]
    public async Task GetDetail_PublishedListing_DoesNotExposeLandlordPhone()
    {
        var response = await _client.GetAsync($"/api/v1/listings/{await SeededIdAsync(0)}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(detail.TryGetProperty("landlordPhone", out _), Is.False);
            Assert.That(detail.GetProperty("landlordName").GetString(), Is.EqualTo("Landlord 0"));
            Assert.That(detail.GetProperty("roomType").GetString(), Is.EqualTo("apartment"));
            Assert.That(detail.GetProperty("status").GetString(), Is.EqualTo("published"));
        });
    }

    [Test]
    public async Task GetDetail_ListingWithoutAmenitiesOrRules_ReturnsEmptyArrays()
    {
        var detail = await GetDetailAsync(await SeededIdAsync(1));

        Assert.Multiple(() =>
        {
            Assert.That(detail.Description, Is.EqualTo("Test listing 1 description"));
            Assert.That(detail.Amenities, Is.Not.Null);
            Assert.That(detail.Amenities, Is.Empty);
            Assert.That(detail.Rules, Is.Not.Null);
            Assert.That(detail.Rules, Is.Empty);
        });
    }

    [TestCase(20)]
    [TestCase(21)]
    [TestCase(22)]
    [TestCase(23)]
    [TestCase(24)]
    public async Task GetDetail_NonPublishedListing_ReturnsGone(int listingIndex)
    {
        var response = await _client.GetAsync($"/api/v1/listings/{await SeededIdAsync(listingIndex)}");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(410));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("listing_unavailable"));
        });
    }

    [Test]
    public async Task GetDetail_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("listing_not_found"));
    }

    [Test]
    public async Task GetDetail_MalformedId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/listings/not-a-guid");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task<ListingDetailDto> GetDetailAsync(Guid id)
    {
        var url = $"/api/v1/listings/{id}";
        var response = await _client.GetAsync(url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), url);

        var detail = await response.Content.ReadFromJsonAsync<ListingDetailDto>(ApiJson.Options);
        Assert.That(detail, Is.Not.Null);
        return detail!;
    }

    // Test listing 0 is seeded with Guid.Empty, which EF Core replaces with a generated key,
    // so the persisted id is resolved from the database instead of from TestListingsSeed.IdFor.
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
