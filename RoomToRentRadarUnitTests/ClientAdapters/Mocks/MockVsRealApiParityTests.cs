using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Mocks;

/// <summary>
/// T00.14 Phase 8 (AC-4): MockRoomGateway returns exactly what HttpRoomGateway returns from the real
/// API (WebApplicationFactory, demo seed on) — every RoomCardDto field via record equality.
/// </summary>
[TestFixture]
public class MockVsRealApiParityTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private ServiceProvider _provider = null!;
    private IRoomGateway _httpGateway = null!;
    private readonly MockRoomGateway _mockGateway = new();

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
        _httpGateway = _provider.GetRequiredService<IRoomGateway>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
        _factory.Dispose();
    }

    private static IEnumerable<TestCaseData> ParityCases()
    {
        yield return new TestCaseData(new RoomSearchRequest())
            .SetName("SearchRoomsAsync_MockMatchesRealApi_DefaultRequest");
        yield return new TestCaseData(new RoomSearchRequest(CampusId: "TNUE"))
            .SetName("SearchRoomsAsync_MockMatchesRealApi_TnueCampus");
        yield return new TestCaseData(new RoomSearchRequest(
                RoomTypes: new[] { RoomType.BoardingRoom }, MaxDistanceKm: 5, Sort: "price_desc"))
            .SetName("SearchRoomsAsync_MockMatchesRealApi_FilteredAndSorted");
    }

    [TestCaseSource(nameof(ParityCases))]
    [CancelAfter(30000)]
    public async Task SearchRoomsAsync_MockMatchesRealApi(RoomSearchRequest request)
    {
        var real = await _httpGateway.SearchRoomsAsync(request);
        var mock = await _mockGateway.SearchRoomsAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(mock.TotalCount, Is.EqualTo(real.TotalCount));
            Assert.That(mock.PageNumber, Is.EqualTo(real.PageNumber));
            Assert.That(mock.PageSize, Is.EqualTo(real.PageSize));
            // RoomCardDto is a record: collection equality compares every field of every card.
            Assert.That(mock.Items, Is.EqualTo(real.Items));
        });
    }

    [Test]
    [CancelAfter(30000)]
    public async Task GetRoomDetailAsync_SeedRoom_BothRealAndMockReturnAvailableDetail()
    {
        var real = await _httpGateway.GetRoomDetailAsync("seed-z115");
        var mock = await _mockGateway.GetRoomDetailAsync("seed-z115");

        Assert.Multiple(() =>
        {
            Assert.That(real.Id, Is.EqualTo(mock.Id));
            Assert.That(real.Title, Is.EqualTo(mock.Title));
            Assert.That(real.MonthlyRentVnd, Is.EqualTo(mock.MonthlyRentVnd));
            Assert.That(real.DepositVnd, Is.EqualTo(mock.DepositVnd));
            Assert.That(real.AreaM2, Is.EqualTo(mock.AreaM2));
            Assert.That(real.RoomType, Is.EqualTo(mock.RoomType));
            Assert.That(real.Status, Is.EqualTo(mock.Status));
            Assert.That(real.IsVerified, Is.EqualTo(mock.IsVerified));
            Assert.That(real.Address, Is.EqualTo(mock.Address));
            Assert.That(real.Location, Is.EqualTo(mock.Location));
            Assert.That(mock.HouseRules, Is.Not.Null);
            Assert.That(mock.Photos, Has.Count.EqualTo(3));
            Assert.That(mock.Safety.Score, Is.EqualTo(4));
        });
    }

    // Private copy of ClientAdaptersEndToEndTests.CreateFactory, with SeedDemoRooms=true (PLAN Phase 6/8).
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Sqlite:SeedExtendedDemoData", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
}