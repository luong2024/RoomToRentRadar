using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class NotificationsEndpointTests
{
    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;

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
        var user = await TestUsersSeed.SeedAsync(dbContext);
        _userId = user.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetNotifications_Anonymous_ReturnsUnauthorizedProblem()
    {
        var response = await _client.GetAsync("/api/v1/notifications");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task GetNotifications_WithoutNotifications_ReturnsEmptyList()
    {
        var response = await GetAsync(await AccessTokenAsync(), limit: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>(ApiJson.Options);
        Assert.That(notifications, Is.Empty);
    }

    [Test]
    public async Task GetNotifications_ReturnsNewestFirst()
    {
        var older = await AddNotificationAsync("Cũ", DateTime.UtcNow.AddMinutes(-10), _userId);
        var newer = await AddNotificationAsync("Mới", DateTime.UtcNow, _userId);

        var response = await GetAsync(await AccessTokenAsync(), limit: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Count.EqualTo(2));
            Assert.That(notifications![0].Id, Is.EqualTo(newer));
            Assert.That(notifications[1].Id, Is.EqualTo(older));
            Assert.That(notifications[0].Title, Is.EqualTo("Mới"));
            Assert.That(notifications[0].Body, Is.EqualTo("Nội dung"));
        });
    }

    [Test]
    public async Task GetNotifications_RespectsLimit()
    {
        var token = await AccessTokenAsync();
        await AddNotificationAsync("Một", DateTime.UtcNow.AddMinutes(-3), _userId);
        await AddNotificationAsync("Hai", DateTime.UtcNow.AddMinutes(-2), _userId);
        await AddNotificationAsync("Ba", DateTime.UtcNow.AddMinutes(-1), _userId);

        var response = await GetAsync(token, limit: 2);

        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>(ApiJson.Options);
        Assert.That(notifications, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetNotifications_DoesNotLeakAnotherUsersNotifications()
    {
        var foreignUserId = Guid.NewGuid();
        await AddForeignUserAsync(foreignUserId);
        await AddNotificationAsync("Của tôi", DateTime.UtcNow, _userId);
        await AddNotificationAsync("Của người khác", DateTime.UtcNow, foreignUserId);

        var response = await GetAsync(await AccessTokenAsync(), limit: null);

        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications![0].Title, Is.EqualTo("Của tôi"));
        });
    }

    [TestCase(0)]
    [TestCase(51)]
    public async Task GetNotifications_InvalidLimit_ReturnsInvalidLimitProblem(int limit)
    {
        var response = await GetAsync(await AccessTokenAsync(), limit);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_limit");
    }

    private async Task<HttpResponseMessage> GetAsync(string accessToken, int? limit)
    {
        var path = limit is null
            ? "/api/v1/notifications"
            : $"/api/v1/notifications?limit={limit}";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<string> AccessTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(TestUsersSeed.Email, TestUsersSeed.Password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        return login!.AccessToken;
    }

    private async Task<Guid> AddNotificationAsync(string title, DateTime createdAt, Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SavedSearchId = Guid.NewGuid(),
            ListingId = Guid.NewGuid(),
            Title = title,
            Body = "Nội dung",
            CreatedAt = createdAt
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        return notification.Id;
    }

    private async Task AddForeignUserAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = $"foreign-{userId:N}@example.test",
            DisplayName = "Foreign Tester",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
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
