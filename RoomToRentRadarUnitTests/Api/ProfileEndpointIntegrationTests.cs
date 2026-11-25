using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// T13.4 — <c>PATCH /api/me/profile</c> through the real HTTP pipeline (BR-CMP-02, GUARDRAILS 1.5.1).
/// Each test gets its own factory to isolate the SQLite in-memory DB.
/// </summary>
[TestFixture]
public class ProfileEndpointIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";
    private const string Email = "student@ictu.edu.vn";
    private const string Password = "StrongPass123";

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
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
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

    private async Task<string> RegisterStudentAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterUserRequest(Email, Password, "Nguyen Van Sinh Vien", "Student", "0912345678", "DTC225180"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private Task<HttpResponseMessage> PatchAsync(string json, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/me/profile")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private async Task<JsonDocument> GetMeAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errorCode").GetString();
    }

    [Test]
    public async Task Patch_ChangeCampus_Returns200_AndGetMeShowsIt()
    {
        var token = await RegisterStudentAsync();

        var response = await PatchAsync("""{"selectedCampusId":"TNUT"}""", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("selectedCampusId").GetString(), Is.EqualTo("TNUT"));
        using var me = await GetMeAsync(token);
        Assert.That(me.RootElement.GetProperty("selectedCampusId").GetString(), Is.EqualTo("TNUT"));
    }

    [Test]
    public async Task Patch_WithoutToken_Returns401ProblemJson()
    {
        var response = await PatchAsync("""{"selectedCampusId":"TNUT"}""", token: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await ReadErrorCodeAsync(response), Is.Not.Empty);
    }

    [Test]
    public async Task Patch_UnknownCampus_Returns400CampusNotFound()
    {
        var token = await RegisterStudentAsync();

        var response = await PatchAsync("""{"selectedCampusId":"tnut"}""", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await ReadErrorCodeAsync(response), Is.EqualTo("CAMPUS_NOT_FOUND"));
    }

    [Test]
    public async Task Patch_LanguageFr_Returns400ValidationFailed()
    {
        var token = await RegisterStudentAsync();

        var response = await PatchAsync("""{"preferredLanguage":"fr"}""", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await ReadErrorCodeAsync(response), Is.EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task Patch_ExtraRoleAndIsVerifiedFields_AreIgnored_RoleStaysStudent()
    {
        var token = await RegisterStudentAsync();

        var response = await PatchAsync(
            """{"role":"Admin","isVerified":false,"email":"evil@example.com","id":"seed-admin","selectedCampusId":"TUMP"}""",
            token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var me = await GetMeAsync(token);
        Assert.Multiple(() =>
        {
            Assert.That(me.RootElement.GetProperty("role").GetString(), Is.EqualTo("Student"));
            Assert.That(me.RootElement.GetProperty("isVerified").GetBoolean(), Is.True);
            Assert.That(me.RootElement.GetProperty("email").GetString(), Is.EqualTo(Email));
            Assert.That(me.RootElement.GetProperty("selectedCampusId").GetString(), Is.EqualTo("TUMP"));
        });
    }

    [Test]
    public async Task Patch_EmptyObject_Returns200Unchanged()
    {
        var token = await RegisterStudentAsync();

        var response = await PatchAsync("{}", token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("selectedCampusId").GetString(), Is.EqualTo("ICTU"));
    }

    [TestCase("{\"selectedCampusId\":")]
    [TestCase("")]
    public async Task Patch_MalformedOrEmptyBody_Returns400(string json)
    {
        var token = await RegisterStudentAsync();

        var response = await PatchAsync(json, token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await ReadErrorCodeAsync(response), Is.EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task Login_AfterCampusChange_ProfileInTokenResponseCarriesNewCampus()
    {
        var token = await RegisterStudentAsync();
        await PatchAsync("""{"selectedCampusId":"TNUE"}""", token);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, Password));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(
            body.RootElement.GetProperty("user").GetProperty("selectedCampusId").GetString(),
            Is.EqualTo("TNUE"));
    }

    [Test]
    public async Task Patch_TwoConsecutiveUpdates_BothSucceed()
    {
        var token = await RegisterStudentAsync();

        var first = await PatchAsync("""{"selectedCampusId":"TNUT"}""", token);
        var second = await PatchAsync("""{"selectedCampusId":"ICTU","preferredLanguage":"vi"}""", token);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
        using var me = await GetMeAsync(token);
        Assert.That(me.RootElement.GetProperty("preferredLanguage").GetString(), Is.EqualTo("vi"));
    }
}
