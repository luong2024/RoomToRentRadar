using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class AuthEndpointTests
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
        await TestUsersSeed.SeedAsync(dbContext);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Login_ValidCredentials_ReturnsAccessToken()
    {
        var response = await LoginAsync(TestUsersSeed.Email, TestUsersSeed.Password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(login, Is.Not.Null);
            Assert.That(login!.AccessToken, Is.Not.Empty);
            Assert.That(login.DisplayName, Is.EqualTo(TestUsersSeed.DisplayName));
            Assert.That(login.ExpiresAt, Is.GreaterThan(DateTime.UtcNow));
        });
    }

    [Test]
    public async Task Login_IsCaseInsensitiveForEmail()
    {
        var response = await LoginAsync("DEMO@EXAMPLE.TEST", TestUsersSeed.Password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Login_WrongPassword_ReturnsUnauthorizedProblem()
    {
        var response = await LoginAsync(TestUsersSeed.Email, "wrong-password");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Test]
    public async Task Login_UnknownEmail_ReturnsUnauthorizedProblem()
    {
        var response = await LoginAsync("missing@example.test", TestUsersSeed.Password);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Test]
    public async Task Login_MissingPassword_ReturnsBadRequestProblem()
    {
        var response = await LoginAsync(TestUsersSeed.Email, string.Empty);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_login_request");
    }

    [Test]
    public async Task Logout_ValidToken_InvalidatesTheSession()
    {
        var login = await LoginAndReadAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var logout = await _client.SendAsync(request);
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var secondLogout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        secondLogout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await _client.SendAsync(secondLogout);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Logout_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var response = await _client.PostAsync("/api/v1/auth/logout", content: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task DemoLogin_OutsideDevelopment_ReturnsNotFound()
    {
        var response = await _client.PostAsync("/api/v1/auth/demo-login", content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            ApiJson.Options);
        return response;
    }

    private async Task<LoginResponse> LoginAndReadAsync()
    {
        var response = await LoginAsync(TestUsersSeed.Email, TestUsersSeed.Password);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login, Is.Not.Null);
        return login!;
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(status));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo(code));
    }
}
