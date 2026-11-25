using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class DemoAccountsLoginIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestCase("student@roomtorentradar.local", "Student@123", "Student", true)]
    [TestCase("seed-landlord@roomtorentradar.local", "Landlord@123", "Landlord", true)]
    [TestCase("landlord.pending@roomtorentradar.local", "Landlord@123", "Landlord", false)]
    [TestCase("admin@roomtorentradar.local", "Admin@12345", "Admin", true)]
    public async Task Login_EachDemoAccount_Returns200WithExpectedRoleAndIsVerified(
        string email,
        string password,
        string expectedRole,
        bool expectedIsVerified)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        var user = root.GetProperty("user");

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("accessToken").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(root.GetProperty("refreshToken").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(user.GetProperty("email").GetString(), Is.EqualTo(email));
            Assert.That(user.GetProperty("role").GetString(), Is.EqualTo(expectedRole));
            Assert.That(user.GetProperty("isVerified").GetBoolean(), Is.EqualTo(expectedIsVerified));
        });
    }

    [Test]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("student@roomtorentradar.local", "WrongPassword123"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(
            body.RootElement.GetProperty("errorCode").GetString(),
            Is.EqualTo(ProblemCodes.InvalidCredentials));
    }

    [Test]
    public async Task AdminAccount_CanCallAuditLogs_StudentGets403()
    {
        var adminToken = await LoginAndGetAccessTokenAsync("admin@roomtorentradar.local", "Admin@12345");
        var studentToken = await LoginAndGetAccessTokenAsync("student@roomtorentradar.local", "Student@123");

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit-logs");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminResponse = await _client.SendAsync(adminRequest);

        using var studentRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit-logs");
        studentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var studentResponse = await _client.SendAsync(studentRequest);

        Assert.Multiple(() =>
        {
            Assert.That(adminResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(studentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    private async Task<string> LoginAndGetAccessTokenAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }
}
