using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ModerationReportDetailEndpointTests
{
    private const string ModeratorEmail = "moderator@example.test";
    private const string AdminEmail = "admin@example.test";
    private static readonly Guid ModeratorId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly DateTime BaseTime = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;
    private Guid _listingId;

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
        await TestListingsSeed.SeedAsync(dbContext);
        _listingId = await dbContext.Listings
            .Where(listing => listing.Title == "Test listing 0")
            .Select(listing => listing.Id)
            .SingleAsync();
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
    public async Task GetReport_EnforcesRoleMatrix(
        string? role,
        HttpStatusCode expectedStatus,
        string? expectedCode)
    {
        var reportId = await SeedReportAsync();
        var response = await GetReportAsync(reportId, await AccessTokenForRoleAsync(role));

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
    public async Task GetReport_ReturnsDetailsAndClaimState()
    {
        var reportId = await SeedReportAsync(
            ListingReportStatus.InReview,
            ListingReportReason.Scam,
            "Deposit is fake",
            ModeratorId,
            DateTime.UtcNow.AddMinutes(10));

        var response = await GetReportAsync(reportId, await AccessTokenForRoleAsync("moderator"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var report = await response.Content.ReadFromJsonAsync<ModerationReportDetailDto>(ApiJson.Options);
        Assert.That(report, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(report!.Id, Is.EqualTo(reportId));
            Assert.That(report.ListingId, Is.EqualTo(_listingId));
            Assert.That(report.ListingTitle, Is.EqualTo("Test listing 0"));
            Assert.That(report.ListingStatus, Is.EqualTo(ListingStatus.Published));
            Assert.That(report.ReporterId, Is.EqualTo(_userId));
            Assert.That(report.Reason, Is.EqualTo(ListingReportReason.Scam));
            Assert.That(report.Details, Is.EqualTo("Deposit is fake"));
            Assert.That(report.Status, Is.EqualTo(ListingReportStatus.InReview));
            Assert.That(report.ClaimedByUserId, Is.EqualTo(ModeratorId));
            Assert.That(report.ClaimedUntilUtc, Is.Not.Null);
            Assert.That(report.CreatedAt, Is.EqualTo(BaseTime));
        });
    }

    [Test]
    public async Task GetReport_UnknownId_ReturnsNotFound()
    {
        var response = await GetReportAsync(Guid.NewGuid(), await AccessTokenForRoleAsync("moderator"));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "report_not_found");
    }

    private async Task<HttpResponseMessage> GetReportAsync(Guid reportId, string? accessToken)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/moderation/reports/{reportId}");
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
        ListingReportStatus status = ListingReportStatus.Open,
        ListingReportReason reason = ListingReportReason.Spam,
        string? details = null,
        Guid? claimedByUserId = null,
        DateTime? claimedUntilUtc = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = new ListingReport
        {
            Id = Guid.NewGuid(),
            ListingId = _listingId,
            ReporterId = _userId,
            Reason = reason,
            Details = details,
            Status = status,
            ClaimedByUserId = claimedByUserId,
            ClaimedUntilUtc = claimedUntilUtc,
            CreatedAt = BaseTime
        };
        dbContext.ListingReports.Add(report);
        await dbContext.SaveChangesAsync();
        return report.Id;
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
