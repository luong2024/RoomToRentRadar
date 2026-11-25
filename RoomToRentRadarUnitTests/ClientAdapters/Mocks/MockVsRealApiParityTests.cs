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
            Assert.That(real.Description, Is.EqualTo(mock.Description));
            Assert.That(real.MonthlyRentVnd, Is.EqualTo(mock.MonthlyRentVnd));
            Assert.That(real.DepositVnd, Is.EqualTo(mock.DepositVnd));
            Assert.That(real.AreaM2, Is.EqualTo(mock.AreaM2));
            Assert.That(real.MaxOccupants, Is.EqualTo(mock.MaxOccupants));
            Assert.That(real.Furnishing, Is.EqualTo(mock.Furnishing));
            Assert.That(real.AvailableFrom, Is.EqualTo(mock.AvailableFrom));
            Assert.That(real.RoomType, Is.EqualTo(mock.RoomType));
            Assert.That(real.Status, Is.EqualTo(mock.Status));
            Assert.That(real.IsVerified, Is.EqualTo(mock.IsVerified));
            Assert.That(real.Address, Is.EqualTo(mock.Address));
            Assert.That(real.Location, Is.EqualTo(mock.Location));
            Assert.That(real.DistanceKm, Is.EqualTo(mock.DistanceKm).Within(0.001));
            Assert.That(real.Utilities.ElectricityPriceVndPerKwh, Is.EqualTo(mock.Utilities.ElectricityPriceVndPerKwh));
            Assert.That(real.Utilities.WaterPriceVnd, Is.EqualTo(mock.Utilities.WaterPriceVnd));
            Assert.That(real.Utilities.WaterPriceUnit, Is.EqualTo(mock.Utilities.WaterPriceUnit));
            Assert.That(real.Utilities.InternetFeeVnd, Is.EqualTo(mock.Utilities.InternetFeeVnd));
            Assert.That(real.Utilities.ParkingFeeVnd, Is.EqualTo(mock.Utilities.ParkingFeeVnd));
            Assert.That(real.Utilities.OtherFeesNote, Is.EqualTo(mock.Utilities.OtherFeesNote));
            Assert.That(real.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_060_000L));
            Assert.That(real.Landlord.Id, Is.EqualTo(mock.Landlord.Id));
            Assert.That(real.Landlord.PhoneNumber, Is.EqualTo(mock.Landlord.PhoneNumber));
            Assert.That(real.Landlord.ZaloUrl, Is.EqualTo(mock.Landlord.ZaloUrl));
            Assert.That(real.PublishedAtUtc, Is.EqualTo(mock.PublishedAtUtc));
            Assert.That(real.Safety.Score, Is.Zero, "Extended seed is disabled here, so real safety score starts at 0");
            Assert.That(mock.HouseRules, Is.Not.Null);
            Assert.That(mock.Photos, Has.Count.EqualTo(3));
        });
    }

    [Test]
    [CancelAfter(30000)]
    public async Task GetRoomDetailAsync_SeedRoomWithExtendedDemo_RealApiMatchesSafetyAndUtilities()
    {
        using var extendedFactory = CreateFactory(seedExtendedDemoData: true);
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => extendedFactory.Server.CreateHandler()));
        services.AddClientAdapters(new ApiOptions { BaseUrl = new Uri("http://localhost") });
        using var provider = services.BuildServiceProvider();
        var extendedGateway = provider.GetRequiredService<IRoomGateway>();

        var real = await extendedGateway.GetRoomDetailAsync("seed-z115");
        var mock = await _mockGateway.GetRoomDetailAsync("seed-z115");

        Assert.Multiple(() =>
        {
            Assert.That(real.Amenities, Is.Not.Empty);
            Assert.That(real.HouseRules, Is.Not.Null);
            Assert.That(real.Safety.IsVerified, Is.EqualTo(mock.Safety.IsVerified));
            Assert.That(real.Safety.Score, Is.EqualTo(3));
            Assert.That(real.Utilities.OtherFeesNote, Is.EqualTo(mock.Utilities.OtherFeesNote));
            Assert.That(real.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_060_000L));
        });
    }

    // Private copy of ClientAdaptersEndToEndTests.CreateFactory, with SeedDemoRooms=true (PLAN Phase 6/8).
    private static WebApplicationFactory<Program> CreateFactory(bool seedExtendedDemoData = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Sqlite:SeedExtendedDemoData", seedExtendedDemoData ? "true" : "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
}