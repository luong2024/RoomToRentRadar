using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// T00.7 — selectedCampusId / preferredLanguage defaults (BR-CMP-02, BR-I18N-02)
/// through the real HTTP pipeline. Each test gets its own factory to isolate the SQLite in-memory DB.
/// </summary>
[TestFixture]
public class AuthProfileDefaultsIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // Same UseSetting pattern as RoomsEndpointIntegrationTests: host configuration reaches builder.Configuration.
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });

    private static RegisterUserRequest StudentRequest() =>
        new(
            Email: "student@ictu.edu.vn",
            Password: "StrongPass123",
            FullName: "Nguyen Van Sinh Vien",
            Role: "Student",
            PhoneNumber: "0912345678",
            StudentCode: "DTC225180");

    private static RegisterUserRequest LandlordRequest() =>
        new(
            Email: "landlord@gmail.com",
            Password: "StrongPass123",
            FullName: "Tran Van Chu Tro",
            Role: "Landlord",
            PhoneNumber: "0987654321");

    private async Task<JsonDocument> RegisterAsync(RegisterUserRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Register_Student_ReturnsDefaultCampusAndLanguage()
    {
        using var body = await RegisterAsync(StudentRequest());
        var user = body.RootElement.GetProperty("user");

        Assert.Multiple(() =>
        {
            Assert.That(user.GetProperty("selectedCampusId").GetString(), Is.EqualTo("ICTU"));
            Assert.That(user.GetProperty("preferredLanguage").GetString(), Is.EqualTo("en"));
        });
    }

    [Test]
    public async Task Register_Landlord_ReturnsDefaults()
    {
        using var body = await RegisterAsync(LandlordRequest());
        var user = body.RootElement.GetProperty("user");

        Assert.Multiple(() =>
        {
            Assert.That(user.GetProperty("selectedCampusId").GetString(), Is.EqualTo("ICTU"));
            Assert.That(user.GetProperty("preferredLanguage").GetString(), Is.EqualTo("en"));
        });
    }

    [Test]
    public async Task Login_ReturnsDefaultCampusAndLanguage()
    {
        using var registerBody = await RegisterAsync(StudentRequest());

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(Email: "student@ictu.edu.vn", Password: "StrongPass123"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = body.RootElement.GetProperty("user");

        Assert.Multiple(() =>
        {
            Assert.That(user.GetProperty("selectedCampusId").GetString(), Is.EqualTo("ICTU"));
            Assert.That(user.GetProperty("preferredLanguage").GetString(), Is.EqualTo("en"));
        });
    }

    [Test]
    public async Task GetMe_ReturnsDefaultCampusAndLanguage()
    {
        using var registerBody = await RegisterAsync(StudentRequest());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("selectedCampusId").GetString(), Is.EqualTo("ICTU"));
            Assert.That(root.GetProperty("preferredLanguage").GetString(), Is.EqualTo("en"));
        });
    }

    [Test]
    public async Task Login_TwiceSequentially_BothSucceedAndVersionAdvances()
    {
        using var registerBody = await RegisterAsync(StudentRequest());

        var loginRequest = new LoginRequest(Email: "student@ictu.edu.vn", Password: "StrongPass123");
        var firstLogin = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var secondLogin = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.Multiple(() =>
        {
            Assert.That(firstLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(secondLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });

        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync("student@ictu.edu.vn");

        Assert.That(user, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(user!.Version, Is.GreaterThan(1), "Each login persists via UpdateAsync, which bumps Version.");
            Assert.That(user.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(user.PreferredLanguage, Is.EqualTo("en"));
        });
    }

    [Test]
    public async Task Register_ResponseJson_DoesNotExposePascalCaseDuplicates()
    {
        using var body = await RegisterAsync(StudentRequest());
        var user = body.RootElement.GetProperty("user");

        Assert.Multiple(() =>
        {
            Assert.That(user.TryGetProperty("selectedCampusId", out _), Is.True);
            Assert.That(user.TryGetProperty("preferredLanguage", out _), Is.True);
            Assert.That(user.TryGetProperty("SelectedCampusId", out _), Is.False);
            Assert.That(user.TryGetProperty("PreferredLanguage", out _), Is.False);
        });
    }
}