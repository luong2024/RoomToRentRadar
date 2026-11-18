using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// T00.14 Phase 6 (AC-2): HttpRoomGateway through the real AddClientAdapters pipeline against the
/// real API in-process (WebApplicationFactory, demo seed on). No real-API campus test yet:
/// GET /api/campuses does not exist until T00.8 (PR #59) merges (PLAN Q4).
/// </summary>
[TestFixture]
public class HttpRoomGatewayRealApiTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private ServiceProvider _provider = null!;
    private IRoomGateway _gateway = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => _factory.Server.CreateHandler()));
        services.AddClientAdapters(new ApiOptions { BaseUrl = new Uri("http://localhost") });
        _provider = services.BuildServiceProvider();
        _gateway = _provider.GetRequiredService<IRoomGateway>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
        _factory.Dispose();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task SearchRoomsAsync_AgainstRealApi_Returns5SeedCardsOrderedByDistance()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest());

        Assert.That(result.TotalCount, Is.EqualTo(5));
        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[]
        {
            "seed-z115", "seed-thao-nguyen", "seed-tan-thinh", "seed-thinh-dan", "seed-ktx-32"
        }));
    }

    [Test]
    [CancelAfter(30000)]
    public void SearchRoomsAsync_InvalidSort_ThrowsApiException_InvalidSortOption()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: "bogus")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.InvalidSortOption));
    }

    [Test]
    [CancelAfter(30000)]
    public void SearchRoomsAsync_UnknownCampus_ThrowsApiException_CampusNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.SearchRoomsAsync(new RoomSearchRequest(CampusId: "NOPE")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task GetRoomDetailAsync_SeedRoom_ReturnsDetailFromRealApi()
    {
        var detail = await _gateway.GetRoomDetailAsync("seed-z115");

        Assert.Multiple(() =>
        {
            Assert.That(detail.Id, Is.EqualTo("seed-z115"));
            Assert.That(detail.Title, Does.Contain("Z115"));
            Assert.That(detail.Status, Is.EqualTo(Domain.Enums.ListingStatus.Available));
            Assert.That(detail.IsVerified, Is.True);
        });
    }

    [Test]
    [CancelAfter(30000)]
    public void GetRoomDetailAsync_UnknownRoom_ThrowsApiException_RoomNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.GetRoomDetailAsync("non-existent-room-id"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task GetSimilarRoomsAsync_SeedRoom_ReturnsSimilarRoomsFromRealApi()
    {
        var similar = await _gateway.GetSimilarRoomsAsync("seed-z115");

        Assert.Multiple(() =>
        {
            Assert.That(similar, Is.Not.Null);
            Assert.That(similar.All(r => r.Id != "seed-z115"), Is.True, "Must exclude target room itself");
        });
    }

    // Private copy of ClientAdaptersEndToEndTests.CreateFactory, with SeedDemoRooms=true (PLAN Phase 6).
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
}