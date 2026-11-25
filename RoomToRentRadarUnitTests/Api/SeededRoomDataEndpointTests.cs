using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// T00.10 AC D2-D4: the seeded rooms reach the client with photos, amenities, safety features,
/// house rules and furnishing through the real HTTP pipeline.
/// </summary>
[TestFixture]
public class SeededRoomDataEndpointTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await _client.GetAsync(url);
        return await ApiResponseReader.ReadAsync<T>(response);
    }

    // The configured public base URL is not the test server, so only the path is requested.
    private async Task<(HttpStatusCode Status, string? Type, int Length)> DownloadAsync(string url)
    {
        using var response = await _client.GetAsync(new Uri(url).AbsolutePath);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, bytes.Length);
    }

    [Test]
    public async Task Search_EveryCard_HasADownloadableCoverThumbnailAndFurnishing()
    {
        var page = await GetAsync<PagedResult<RoomCardDto>>("/api/rooms?pageSize=50");

        Assert.That(page.TotalCount, Is.EqualTo(22));
        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => i.CoverThumbUrl), Is.All.Contains("/media/seed/rooms/"));
            Assert.That(page.Items.Select(i => i.CoverThumbUrl), Is.All.EndsWith("-thumb.jpg"));
            Assert.That(page.Items.All(i => i.Furnishing is not null), Is.True);
        });

        var (status, type, length) = await DownloadAsync(page.Items[0].CoverThumbUrl!);
        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(type, Is.EqualTo("image/jpeg"));
            Assert.That(length, Is.LessThanOrEqualTo(60 * 1024));
        });
    }

    [Test]
    public async Task Detail_ReturnsPhotosAmenitiesSafetyAndCurrentHouseRules()
    {
        var detail = await GetAsync<RoomDetailDto>("/api/rooms/demo-02");

        Assert.Multiple(() =>
        {
            Assert.That(detail.Photos.Select(p => p.SortOrder), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(detail.Photos.All(p => p.Kind == PhotoKind.Standard), Is.True);
            Assert.That(detail.Amenities, Is.Not.Empty);
            Assert.That(detail.Safety.Score, Is.EqualTo(detail.Safety.Features.Count));
            Assert.That(detail.Safety.Score, Is.GreaterThan(0));
            Assert.That(detail.HouseRules!.VersionNumber, Is.EqualTo(2));
        });

        var (status, type, _) = await DownloadAsync(detail.Photos[0].MediumUrl);
        Assert.That((status, type), Is.EqualTo((HttpStatusCode.OK, "image/jpeg")));
    }

    [Test]
    public async Task Similar_CardsCarryACover()
    {
        var similar = await GetAsync<List<RoomCardDto>>("/api/rooms/seed-z115/similar");

        Assert.That(similar, Is.Not.Empty);
        Assert.That(similar.Select(i => i.CoverThumbUrl), Is.All.Contains("/media/seed/rooms/"));
    }
}
