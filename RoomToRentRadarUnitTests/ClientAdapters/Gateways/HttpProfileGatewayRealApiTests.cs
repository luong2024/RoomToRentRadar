using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>T13.4 AC-6 — profile gateway and campus selection against the real API in-process.</summary>
[TestFixture]
public class HttpProfileGatewayRealApiTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private ServiceProvider _provider = null!;
    private InMemorySessionStore _sessionStore = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
        _sessionStore = new InMemorySessionStore();
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(_sessionStore);
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => _factory.Server.CreateHandler()));
        services.AddClientAdapters(new ApiOptions { BaseUrl = new Uri("http://localhost") });
        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
        _factory.Dispose();
    }

    private async Task SignInNewStudentAsync()
    {
        var tokens = await _provider.GetRequiredService<IAuthGateway>().RegisterAsync(new RegisterUserRequest(
            "student@ictu.edu.vn", "StrongPass123", "Nguyen Van Sinh Vien", "Student", "0912345678", "DTC225180"));
        await _sessionStore.SaveSessionAsync(tokens);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Register_Patch_Me_RoundTripOverRealApi()
    {
        await SignInNewStudentAsync();

        var updated = await _provider.GetRequiredService<IProfileGateway>()
            .UpdateMyProfileAsync(new UpdateMyProfileRequest(null, null, "TNUT", null));
        var me = await _provider.GetRequiredService<IAuthGateway>().GetCurrentUserProfileAsync();

        Assert.Multiple(() =>
        {
            Assert.That(updated.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(me.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(me.FullName, Is.EqualTo("Nguyen Van Sinh Vien"));
        });
    }

    [Test]
    [CancelAfter(30000)]
    public async Task GuestChoice_ThenSignIn_IsUploadedToTheProfile()
    {
        var service = new CampusSelectionService(
            _provider.GetRequiredService<ICampusGateway>(),
            _provider.GetRequiredService<IProfileGateway>(),
            _provider.GetRequiredService<IAuthGateway>(),
            _sessionStore,
            new InMemoryPreferencesStore());
        await service.SelectCampusAsync("TUMP");
        await SignInNewStudentAsync();

        await service.SynchronizeAsync();

        var me = await _provider.GetRequiredService<IAuthGateway>().GetCurrentUserProfileAsync();
        Assert.That(me.SelectedCampusId, Is.EqualTo("TUMP"));
    }
}
