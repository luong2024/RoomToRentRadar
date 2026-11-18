using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Moderation;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ModerationQueueEndpointTests
{
    private const string ModeratorEmail = "moderator@example.test";
    private const string AdminEmail = "admin@example.test";
    private static readonly Guid ModeratorId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Reporter2Id = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid Reporter3Id = new("cccccccc-0000-0000-0000-000000000004");
    private static readonly DateTime BaseTime = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;
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
        var user = await TestUsersSeed.SeedAsync(dbContext);
        _userId = user.Id;
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, ModeratorId, ModeratorEmail, UserRole.Moderator, "Moderator Tester");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, AdminId, AdminEmail, UserRole.Admin, "Admin Tester");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, Reporter2Id, "reporter2@example.test", displayName: "Reporter Two");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, Reporter3Id, "reporter3@example.test", displayName: "Reporter Three");
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
    public async Task GetQueue_GroupsByListingWithCountsReasonsAndReportIds()
    {
        var listingA = _listingIds[0];
        var listingB = _listingIds[1];
        var listingC = _listingIds[2];

        var a1 = await SeedReportAsync(
            listingA, _userId, ListingReportReason.Spam, ListingReportStatus.Open, BaseTime.AddHours(1));
        var a2 = await SeedReportAsync(
            listingA, Reporter2Id, ListingReportReason.Spam, ListingReportStatus.Open, BaseTime.AddHours(2));
        var a3 = await SeedReportAsync(
            listingA, Reporter3Id, ListingReportReason.Scam, ListingReportStatus.InReview, BaseTime.AddHours(3));
        var b1 = await SeedReportAsync(
            listingB, _userId, ListingReportReason.Scam, ListingReportStatus.Open, BaseTime);
        var c1 = await SeedReportAsync(
            listingC, _userId, ListingReportReason.WrongInfo, ListingReportStatus.Open, BaseTime.AddMinutes(90));
        var c2 = await SeedReportAsync(
            listingC, Reporter2Id, ListingReportReason.WrongInfo, ListingReportStatus.Open, BaseTime.AddMinutes(150));
        await SeedReportAsync(
            listingC, Reporter3Id, ListingReportReason.Spam, ListingReportStatus.Resolved, BaseTime);

        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var queue = await response.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        Assert.That(queue, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(queue!.Items.Select(item => item.ListingId), Is.EqualTo(new[] { listingA, listingC, listingB }));
            Assert.That(queue.Items.Select(item => item.TotalReports), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(queue.Pagination.Limit, Is.EqualTo(20));
            Assert.That(queue.Pagination.HasMore, Is.False);
            Assert.That(queue.Pagination.NextCursor, Is.Null);
        });

        var groupA = queue.Items[0];
        Assert.Multiple(() =>
        {
            Assert.That(groupA.Title, Is.EqualTo("Test listing 0"));
            Assert.That(groupA.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(groupA.Reasons.Select(reason => reason.Reason), Is.EqualTo(
                new[] { ListingReportReason.Spam, ListingReportReason.Scam }));
            Assert.That(groupA.Reasons.Select(reason => reason.Count), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(groupA.OldestReportAt, Is.EqualTo(BaseTime.AddHours(1)));
            Assert.That(groupA.ReportIds, Is.EqualTo(new[] { a1, a2, a3 }));
            Assert.That(groupA.ClaimedByUserId, Is.Null);
            Assert.That(groupA.ClaimedUntilUtc, Is.Null);
        });

        Assert.Multiple(() =>
        {
            Assert.That(queue.Items[1].ReportIds, Is.EqualTo(new[] { c1, c2 }));
            Assert.That(queue.Items[1].OldestReportAt, Is.EqualTo(BaseTime.AddMinutes(90)));
            Assert.That(queue.Items[2].ReportIds, Is.EqualTo(new[] { b1 }));
        });
    }

    [Test]
    public async Task GetQueue_IncludesHiddenListings()
    {
        await SetListingStatusAsync(_listingIds[1], ListingStatus.Hidden);
        await SeedReportAsync(
            _listingIds[1], _userId, ListingReportReason.Scam,
            ListingReportStatus.Open, BaseTime);

        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"));
        var queue = await response.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        Assert.That(queue!.Items, Has.Count.EqualTo(1));
        Assert.That(queue.Items[0].Status, Is.EqualTo(ListingStatus.Hidden));
    }

    [Test]
    public async Task GetQueue_ReportsActiveClaimAndIgnoresExpiredClaims()
    {
        var claimedUntil = BaseTime.AddYears(1);
        await SeedReportAsync(
            _listingIds[0], _userId, ListingReportReason.Spam,
            ListingReportStatus.InReview, BaseTime, ModeratorId, claimedUntil);
        await SeedReportAsync(
            _listingIds[1], _userId, ListingReportReason.Spam,
            ListingReportStatus.InReview, BaseTime, ModeratorId, BaseTime.AddMinutes(1));

        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"));
        var queue = await response.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        var claimedGroup = queue!.Items.Single(item => item.ListingId == _listingIds[0]);
        Assert.Multiple(() =>
        {
            Assert.That(claimedGroup.ClaimedByUserId, Is.EqualTo(ModeratorId));
            Assert.That(claimedGroup.ClaimedUntilUtc, Is.EqualTo(claimedUntil));
        });

        var expiredGroup = queue.Items.Single(item => item.ListingId == _listingIds[1]);
        Assert.Multiple(() =>
        {
            Assert.That(expiredGroup.ClaimedByUserId, Is.Null);
            Assert.That(expiredGroup.ClaimedUntilUtc, Is.Null);
        });
    }

    [Test]
    public async Task GetQueue_WalksCursorWithoutDuplicates()
    {
        await SeedReportAsync(
            _listingIds[0], _userId, ListingReportReason.Spam,
            ListingReportStatus.Open, BaseTime.AddHours(2));
        await SeedReportAsync(
            _listingIds[0], Reporter2Id, ListingReportReason.Spam,
            ListingReportStatus.Open, BaseTime.AddHours(3));
        await SeedReportAsync(
            _listingIds[1], _userId, ListingReportReason.Scam,
            ListingReportStatus.Open, BaseTime);
        await SeedReportAsync(
            _listingIds[2], _userId, ListingReportReason.Offensive,
            ListingReportStatus.Open, BaseTime.AddHours(1));

        var token = await AccessTokenForRoleAsync("moderator");
        var firstResponse = await GetQueueAsync(token, "limit=2");
        var first = await firstResponse.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        Assert.That(first!.Items, Has.Count.EqualTo(2));
        Assert.That(first.Pagination.HasMore, Is.True);
        Assert.That(first.Pagination.NextCursor, Is.Not.Null);

        var secondResponse = await GetQueueAsync(
            token,
            $"limit=2&cursor={Uri.EscapeDataString(first.Pagination.NextCursor!)}");
        var second = await secondResponse.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        Assert.That(second!.Items, Has.Count.EqualTo(1));
        Assert.That(second.Pagination.HasMore, Is.False);
        Assert.That(second.Pagination.NextCursor, Is.Null);

        var firstIds = first.Items.Select(item => item.ListingId).ToList();
        var allIds = firstIds.Concat(second.Items.Select(item => item.ListingId)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(allIds, Is.Unique);
            Assert.That(allIds, Is.EquivalentTo(new[]
            {
                _listingIds[0],
                _listingIds[1],
                _listingIds[2]
            }));
        });
    }

    [Test]
    public async Task GetQueue_CapsReportIdsPerGroup()
    {
        const int reportCount = ModerationQueueService.MaximumReportIdsPerGroup + 5;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var (hash, salt) = PasswordHasher.Hash(TestUsersSeed.Password);
            for (var index = 0; index < reportCount; index++)
            {
                var reporterId = Guid.NewGuid();
                dbContext.Users.Add(new User
                {
                    Id = reporterId,
                    Email = $"bulk-reporter-{index}@example.test",
                    DisplayName = $"Bulk Reporter {index}",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = UserRole.User,
                    CreatedAt = BaseTime
                });
                dbContext.ListingReports.Add(new ListingReport
                {
                    Id = Guid.NewGuid(),
                    ListingId = _listingIds[0],
                    ReporterId = reporterId,
                    Reason = ListingReportReason.Spam,
                    Status = ListingReportStatus.Open,
                    CreatedAt = BaseTime.AddMinutes(index)
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"));
        var queue = await response.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(queue!.Items, Has.Count.EqualTo(1));
            Assert.That(queue.Items[0].TotalReports, Is.EqualTo(reportCount));
            Assert.That(
                queue.Items[0].ReportIds,
                Has.Count.EqualTo(ModerationQueueService.MaximumReportIdsPerGroup));
        });
    }

    [Test]
    public async Task GetQueue_EmptyQueue_ReturnsEmptyPage()
    {
        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"));
        var queue = await response.Content.ReadFromJsonAsync<ModerationReportQueueDto>(ApiJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(queue!.Items, Is.Empty);
            Assert.That(queue.Pagination.HasMore, Is.False);
            Assert.That(queue.Pagination.NextCursor, Is.Null);
        });
    }

    [TestCase("-1")]
    [TestCase("0")]
    [TestCase("101")]
    public async Task GetQueue_InvalidLimit_ReturnsBadRequest(string limit)
    {
        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"), $"limit={limit}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_limit");
    }

    [Test]
    public async Task GetQueue_NonNumericLimit_ReturnsInvalidParameterFormat()
    {
        var response = await GetQueueAsync(await AccessTokenForRoleAsync("moderator"), "limit=abc");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_parameter_format");
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
        var url = "/api/v1/moderation/reports" + (query is null ? string.Empty : $"?{query}");
        var message = new HttpRequestMessage(HttpMethod.Get, url);
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

    private async Task<Guid> SeedReportAsync(
        Guid listingId,
        Guid reporterId,
        ListingReportReason reason,
        ListingReportStatus status,
        DateTime createdAt,
        Guid? claimedByUserId = null,
        DateTime? claimedUntilUtc = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = new ListingReport
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            ReporterId = reporterId,
            Reason = reason,
            Status = status,
            CreatedAt = createdAt,
            ClaimedByUserId = claimedByUserId,
            ClaimedUntilUtc = claimedUntilUtc
        };
        dbContext.ListingReports.Add(report);
        await dbContext.SaveChangesAsync();
        return report.Id;
    }

    private async Task SetListingStatusAsync(Guid listingId, ListingStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = await dbContext.Listings.SingleAsync(candidate => candidate.Id == listingId);
        listing.Status = status;
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
