using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ModerationListingQueueEndpointTests
{
    private const string ModeratorEmail = "moderator@example.test";
    private const string AdminEmail = "admin@example.test";
    private static readonly Guid ModeratorId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly DateTime BaseTime = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _memberId;
    private Guid[] _listingIds = [];

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
        var member = await TestUsersSeed.SeedAsync(dbContext);
        _memberId = member.Id;
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, ModeratorId, ModeratorEmail, UserRole.Moderator, "Moderator Tester");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, AdminId, AdminEmail, UserRole.Admin, "Admin Tester");
        await TestListingsSeed.SeedAsync(dbContext);
        _listingIds = await LoadListingIdsAsync(dbContext, 3);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestCase(null, HttpStatusCode.Unauthorized, "unauthorized")]
    [TestCase("member", HttpStatusCode.Forbidden, "forbidden")]
    [TestCase("moderator", HttpStatusCode.OK, null)]
    [TestCase("admin", HttpStatusCode.OK, null)]
    public async Task GetQueue_EnforcesRoleMatrix(
        string? role,
        HttpStatusCode expectedStatus,
        string? expectedCode)
    {
        var response = await GetQueueAsync(await AccessTokenForRoleAsync(role));

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        if (expectedCode is null)
        {
            return;
        }

        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo(expectedCode));
    }

    [Test]
    public async Task GetQueue_DefaultsToPendingReviewOldestFirst()
    {
        await SetListingAsync(_listingIds[0], ListingStatus.PendingReview, BaseTime.AddHours(2));
        await SetListingAsync(_listingIds[1], ListingStatus.PendingReview, BaseTime.AddHours(1), _memberId);
        await SetListingAsync(_listingIds[2], ListingStatus.PendingReview, BaseTime.AddHours(1));

        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var queue = await response.Content.ReadFromJsonAsync<ModerationListingQueueDto>(ApiJson.Options);

        Assert.That(queue, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(queue!.Items.Select(item => item.Id), Is.EqualTo(new[]
            {
                _listingIds[1],
                _listingIds[2],
                _listingIds[0]
            }));
            Assert.That(queue.Items.Select(item => item.Title), Is.EqualTo(new[]
            {
                "Test listing 1",
                "Test listing 2",
                "Test listing 0"
            }));
            Assert.That(queue.Items.Select(item => item.Status), Is.All.EqualTo(ListingStatus.PendingReview));
            Assert.That(queue.Items[0].OwnerId, Is.EqualTo(_memberId));
            Assert.That(queue.Items[1].OwnerId, Is.Null);
            Assert.That(queue.Items[0].CreatedAt, Is.EqualTo(BaseTime.AddHours(1)));
            Assert.That(queue.Items[0].UpdatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(queue.Pagination.Limit, Is.EqualTo(20));
            Assert.That(queue.Pagination.HasMore, Is.False);
            Assert.That(queue.Pagination.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task GetQueue_ExplicitStatus_ReturnsOnlyThatStatus()
    {
        var response = await GetQueueAsync(
            await AccessTokenForRoleAsync("moderator"), "status=hidden");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var queue = await response.Content.ReadFromJsonAsync<ModerationListingQueueDto>(ApiJson.Options);

        Assert.That(queue, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(queue!.Items.Select(item => item.Id), Is.EqualTo(new[]
            {
                TestListingsSeed.IdFor(23),
                TestListingsSeed.IdFor(22)
            }));
            Assert.That(queue.Items.Select(item => item.Status), Is.All.EqualTo(ListingStatus.Hidden));
            Assert.That(queue.Pagination.HasMore, Is.False);
        });
    }

    [Test]
    public async Task GetQueue_CursorPaginatesWithoutDuplicates()
    {
        await SetListingAsync(_listingIds[0], ListingStatus.PendingReview, BaseTime.AddHours(2));
        await SetListingAsync(_listingIds[1], ListingStatus.PendingReview, BaseTime.AddHours(1));
        await SetListingAsync(_listingIds[2], ListingStatus.PendingReview, BaseTime.AddHours(1));

        var token = await AccessTokenForRoleAsync("moderator");
        var first = await GetQueueAsync(token, "limit=2");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var firstPage = await first.Content.ReadFromJsonAsync<ModerationListingQueueDto>(ApiJson.Options);

        Assert.That(firstPage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(firstPage!.Items.Select(item => item.Id), Is.EqualTo(new[]
            {
                _listingIds[1],
                _listingIds[2]
            }));
            Assert.That(firstPage.Pagination.Limit, Is.EqualTo(2));
            Assert.That(firstPage.Pagination.HasMore, Is.True);
            Assert.That(firstPage.Pagination.NextCursor, Is.Not.Null);
        });

        var second = await GetQueueAsync(
            token, $"limit=2&cursor={Uri.EscapeDataString(firstPage.Pagination.NextCursor!)}");
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var secondPage = await second.Content.ReadFromJsonAsync<ModerationListingQueueDto>(ApiJson.Options);

        Assert.That(secondPage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(secondPage!.Items.Select(item => item.Id), Is.EqualTo(new[]
            {
                _listingIds[0]
            }));
            Assert.That(secondPage.Pagination.HasMore, Is.False);
            Assert.That(secondPage.Pagination.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task GetQueue_CursorFromDifferentStatus_ReturnsInvalidCursor()
    {
        await SetListingAsync(_listingIds[0], ListingStatus.PendingReview, BaseTime);
        await SetListingAsync(_listingIds[1], ListingStatus.PendingReview, BaseTime.AddHours(1));

        var token = await AccessTokenForRoleAsync("moderator");
        var first = await GetQueueAsync(token, "limit=1");
        var firstPage = await first.Content.ReadFromJsonAsync<ModerationListingQueueDto>(ApiJson.Options);

        var response = await GetQueueAsync(
            token,
            $"status=draft&limit=1&cursor={Uri.EscapeDataString(firstPage!.Pagination.NextCursor!)}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_cursor");
    }

    [Test]
    public async Task GetQueue_InvalidStatus_ReturnsBadRequest()
    {
        var response = await GetQueueAsync(
            await AccessTokenForRoleAsync("moderator"), "status=archived");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_status");
    }

    [TestCase("-1")]
    [TestCase("0")]
    [TestCase("101")]
    public async Task GetQueue_InvalidLimit_ReturnsBadRequest(string limit)
    {
        var response = await GetQueueAsync(
            await AccessTokenForRoleAsync("moderator"), $"limit={limit}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_limit");
    }

    [Test]
    public async Task GetQueue_InvalidCursor_ReturnsBadRequest()
    {
        var response = await GetQueueAsync(
            await AccessTokenForRoleAsync("moderator"), "cursor=not-a-cursor");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_cursor");
    }

    private static async Task<Guid[]> LoadListingIdsAsync(AppDbContext dbContext, int count)
    {
        var ids = new Guid[count];
        for (var index = 0; index < count; index++)
        {
            var title = $"Test listing {index}";
            ids[index] = await dbContext.Listings
                .Where(listing => listing.Title == title)
                .Select(listing => listing.Id)
                .SingleAsync();
        }

        return ids;
    }

    private async Task<HttpResponseMessage> GetQueueAsync(string? accessToken, string? query = null)
    {
        var url = "/api/v1/moderation/listings" + (query is null ? string.Empty : $"?{query}");
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        if (accessToken is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(message);
    }

    private async Task<string?> AccessTokenForRoleAsync(string? role)
    {
        var email = role switch
        {
            "member" => TestUsersSeed.Email,
            "moderator" => ModeratorEmail,
            "admin" => AdminEmail,
            _ => null
        };

        return email is null ? null : await AccessTokenAsync(email);
    }

    private async Task<string> AccessTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, TestUsersSeed.Password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        return login!.AccessToken;
    }

    private async Task SetListingAsync(
        Guid listingId,
        ListingStatus status,
        DateTime createdAt,
        Guid? ownerId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = await dbContext.Listings.SingleAsync(candidate => candidate.Id == listingId);
        listing.Status = status;
        listing.CreatedAt = createdAt;
        listing.OwnerId = ownerId;
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
