using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.SqliteDataAccess;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ProfileEndpointTests
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
    public async Task GetProfile_WithToken_ReturnsAccountDetails()
    {
        var login = await LoginAndReadAsync();

        var response = await SendAsync(HttpMethod.Get, login.AccessToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.UserId, Is.EqualTo(TestUsersSeed.Id));
            Assert.That(profile.Email, Is.EqualTo(TestUsersSeed.Email));
            Assert.That(profile.DisplayName, Is.EqualTo(TestUsersSeed.DisplayName));
            Assert.That(profile.Role, Is.EqualTo(UserRoleWire.Student));
            Assert.That(profile.CreatedAt, Is.EqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task GetProfile_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task UpdateProfile_WithSurroundingWhitespace_TrimsAndPersistsName()
    {
        var login = await LoginAndReadAsync();

        var update = await SendAsync(HttpMethod.Put, login.AccessToken, new UpdateProfileRequest("  Updated Name  "));

        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await update.Content.ReadFromJsonAsync<UserProfileDto>(ApiJson.Options);
        Assert.That(updated!.DisplayName, Is.EqualTo("Updated Name"));

        var fetched = await GetProfileAsync(login.AccessToken);
        Assert.That(fetched!.DisplayName, Is.EqualTo("Updated Name"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Users.SingleAsync(user => user.Id == TestUsersSeed.Id);
        Assert.That(stored.DisplayName, Is.EqualTo("Updated Name"));
    }

    [Test]
    public async Task UpdateProfile_WithEmptyName_ReturnsInvalidDisplayName()
    {
        var login = await LoginAndReadAsync();

        var response = await SendAsync(HttpMethod.Put, login.AccessToken, new UpdateProfileRequest("   "));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_display_name");

        var profile = await GetProfileAsync(login.AccessToken);
        Assert.That(profile!.DisplayName, Is.EqualTo(TestUsersSeed.DisplayName));
    }

    [Test]
    public async Task UpdateProfile_WithTooLongName_ReturnsInvalidDisplayName()
    {
        var login = await LoginAndReadAsync();

        var response = await SendAsync(
            HttpMethod.Put,
            login.AccessToken,
            new UpdateProfileRequest(new string('a', AccountRules.DisplayNameMaxLength + 1)));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_display_name");
    }

    [Test]
    public async Task UpdateProfile_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/v1/auth/me",
            new UpdateProfileRequest("Updated Name"),
            ApiJson.Options);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task GetProfile_AsLandlord_ReturnsLandlordRole()
    {
        const string email = "profile.landlord@example.test";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TestUsersSeed.SeedAdditionalAsync(
                dbContext,
                Guid.NewGuid(),
                email,
                UserRole.Landlord,
                "Profile Landlord");
        }

        var login = await LoginAndReadAsync(email);

        var response = await SendAsync(HttpMethod.Get, login.AccessToken);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(profile!.Role, Is.EqualTo(UserRoleWire.Landlord));
            Assert.That(profile.DisplayName, Is.EqualTo("Profile Landlord"));
        });
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ApiJson.Options);
        }

        return _client.SendAsync(request);
    }

    private async Task<UserProfileDto?> GetProfileAsync(string token)
    {
        var response = await SendAsync(HttpMethod.Get, token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<UserProfileDto>(ApiJson.Options);
    }

    private async Task<LoginResponse> LoginAndReadAsync(string email = TestUsersSeed.Email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, TestUsersSeed.Password),
            ApiJson.Options);
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
