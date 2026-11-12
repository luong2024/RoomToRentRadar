using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class SimilarListingsEndpointTests
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
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetSimilar_PublishedListing_ReturnsOtherPublishedRoomsWithSameBedrooms()
    {
        var subjectId = await SeededIdAsync(2);
        var similar = await GetSimilarAsync(subjectId);

        Assert.Multiple(() =>
        {
            Assert.That(similar, Has.Count.EqualTo(4));
            Assert.That(similar.Select(item => item.Id), Has.No.Member(subjectId));
            Assert.That(similar.Select(item => item.Id), Is.Ordered);
            Assert.That(similar.All(item => item.Bedrooms == 2), Is.True);
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task GetSimilar_Limit_ReturnsAtMostRequestedCount(int limit)
    {
        var similar = await GetSimilarAsync(await SeededIdAsync(2), limit);

        Assert.That(similar, Has.Count.EqualTo(limit));
    }

    [Test]
    public async Task GetSimilar_UnavailableListing_StillReturnsSuggestions()
    {
        var subjectId = TestListingsSeed.IdFor(20);
        var similar = await GetSimilarAsync(subjectId);

        Assert.Multiple(() =>
        {
            Assert.That(similar, Is.Not.Empty);
            Assert.That(similar.All(item => item.Bedrooms == 0), Is.True);
            Assert.That(similar.Select(item => item.Id), Has.No.Member(subjectId));
        });
    }

    [Test]
    public async Task GetSimilar_AppliesPriceAndAreaBoundariesAndExcludesUnpublished()
    {
        var subjectId = await SeededIdAsync(0);

        var boundaryLow = new Guid("44444444-0000-0000-0000-000000000001");
        var boundaryHigh = new Guid("44444444-0000-0000-0000-000000000002");
        var tooCheap = new Guid("44444444-0000-0000-0000-000000000003");
        var tooExpensive = new Guid("44444444-0000-0000-0000-000000000004");
        var tooSmall = new Guid("44444444-0000-0000-0000-000000000005");
        var tooLarge = new Guid("44444444-0000-0000-0000-000000000006");
        var otherBedrooms = new Guid("44444444-0000-0000-0000-000000000007");
        var draftMatch = new Guid("44444444-0000-0000-0000-000000000008");

        await AddListingAsync(boundaryLow, 4_000_000L, 21, bedrooms: 0, ListingStatus.Published);
        await AddListingAsync(boundaryHigh, 6_000_000L, 39, bedrooms: 0, ListingStatus.Published);
        await AddListingAsync(tooCheap, 3_999_999L, 30, bedrooms: 0, ListingStatus.Published);
        await AddListingAsync(tooExpensive, 6_000_001L, 30, bedrooms: 0, ListingStatus.Published);
        await AddListingAsync(tooSmall, 5_000_000L, 20.9, bedrooms: 0, ListingStatus.Published);
        await AddListingAsync(tooLarge, 5_000_000L, 39.1, bedrooms: 0, ListingStatus.Published);
        await AddListingAsync(otherBedrooms, 5_000_000L, 30, bedrooms: 1, ListingStatus.Published);
        await AddListingAsync(draftMatch, 5_000_000L, 30, bedrooms: 0, ListingStatus.Draft);

        var similar = await GetSimilarAsync(subjectId, limit: 10);
        var ids = similar.Select(item => item.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ids, Does.Contain(boundaryLow));
            Assert.That(ids, Does.Contain(boundaryHigh));
            Assert.That(ids, Does.Not.Contain(tooCheap));
            Assert.That(ids, Does.Not.Contain(tooExpensive));
            Assert.That(ids, Does.Not.Contain(tooSmall));
            Assert.That(ids, Does.Not.Contain(tooLarge));
            Assert.That(ids, Does.Not.Contain(otherBedrooms));
            Assert.That(ids, Does.Not.Contain(draftMatch));
            Assert.That(ids, Does.Not.Contain(subjectId));
        });
    }

    [Test]
    public async Task GetSimilar_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}/similar");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("listing_not_found"));
    }

    [TestCase(0)]
    [TestCase(11)]
    public async Task GetSimilar_InvalidLimit_ReturnsBadRequest(int limit)
    {
        var response = await _client.GetAsync(
            $"/api/v1/listings/{await SeededIdAsync(0)}/similar?limit={limit}");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("invalid_limit"));
    }

    private async Task<List<ListingSummaryDto>> GetSimilarAsync(Guid id, int? limit = null)
    {
        var url = limit is null
            ? $"/api/v1/listings/{id}/similar"
            : $"/api/v1/listings/{id}/similar?limit={limit}";
        var response = await _client.GetAsync(url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), url);

        var similar = await response.Content.ReadFromJsonAsync<List<ListingSummaryDto>>(ApiJson.Options);
        Assert.That(similar, Is.Not.Null);
        return similar!;
    }

    private async Task AddListingAsync(
        Guid id,
        long price,
        double area,
        int bedrooms,
        ListingStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        dbContext.Listings.Add(new Listing
        {
            Id = id,
            Title = $"Candidate {id}",
            Description = "Candidate listing",
            Price = price,
            Deposit = price,
            AreaM2 = area,
            Bedrooms = bedrooms,
            Address = "Candidate address",
            Lat = 10.7,
            Lng = 106.7,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            LandlordName = "Candidate landlord",
            LandlordPhone = "0900 000 999"
        });
        await dbContext.SaveChangesAsync();
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
