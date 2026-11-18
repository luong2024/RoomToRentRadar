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
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ModerationListingApprovalEndpointTests
{
    private const string ModeratorEmail = "moderator@example.test";
    private const string AdminEmail = "admin@example.test";
    private static readonly Guid ModeratorId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = new("cccccccc-0000-0000-0000-000000000003");

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
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, ModeratorId, ModeratorEmail, UserRole.Moderator, "Moderator One");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, AdminId, AdminEmail, UserRole.Admin, "Admin Tester");
        await TestListingsSeed.SeedAsync(dbContext);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Approve_AnonymousAndMembersAreRejected()
    {
        var listingId = TestListingsSeed.IdFor(1);
        await SetListingStatusAsync(listingId, ListingStatus.PendingReview);

        var anonymous = await ApproveAsync(listingId, accessToken: null);
        var member = await ApproveAsync(listingId, await AccessTokenAsync(TestUsersSeed.Email));

        await AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized, "unauthorized");
        await AssertProblemAsync(member, HttpStatusCode.Forbidden, "forbidden");
        Assert.That((await ListingAsync(listingId)).Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public async Task Approve_PendingListing_PublishesItAndWritesAudit()
    {
        var listingId = TestListingsSeed.IdFor(1);
        await SetListingStatusAsync(listingId, ListingStatus.PendingReview);

        var response = await ApproveAsync(listingId, await AccessTokenAsync(ModeratorEmail));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var listing = await ListingAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(listing.Version, Is.EqualTo(1));
        });

        var audit = await AuditLogsAsync();
        Assert.That(audit, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(audit[0].Action, Is.EqualTo(ModerationAuditAction.Approve));
            Assert.That(audit[0].ActorUserId, Is.EqualTo(ModeratorId));
            Assert.That(audit[0].ListingId, Is.EqualTo(listingId));
            Assert.That(audit[0].ReportId, Is.Null);
        });

        var publicDetail = await _client.GetAsync($"/api/v1/listings/{listingId}");
        Assert.That(publicDetail.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Approve_AlreadyPublished_IsIdempotentAndWritesNoAudit()
    {
        var listingId = TestListingsSeed.IdFor(3);

        var response = await ApproveAsync(listingId, await AccessTokenAsync(AdminEmail));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var listing = await ListingAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(listing.Version, Is.Zero);
        });
        Assert.That(await AuditLogsAsync(), Is.Empty);
    }

    [TestCase(ListingStatus.Draft)]
    [TestCase(ListingStatus.Hidden)]
    [TestCase(ListingStatus.Removed)]
    public async Task Approve_NonPendingListing_ReturnsInvalidListingTransition(ListingStatus status)
    {
        var listingId = TestListingsSeed.IdFor(3);
        await SetListingStatusAsync(listingId, status);

        var response = await ApproveAsync(listingId, await AccessTokenAsync(AdminEmail));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_listing_transition");
        Assert.That((await ListingAsync(listingId)).Status, Is.EqualTo(status));
        Assert.That(await AuditLogsAsync(), Is.Empty);
    }

    [Test]
    public async Task Approve_UnknownListing_ReturnsNotFound()
    {
        var response = await ApproveAsync(Guid.NewGuid(), await AccessTokenAsync(AdminEmail));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "listing_not_found");
    }

    private async Task<HttpResponseMessage> ApproveAsync(Guid listingId, string? accessToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/moderation/listings/{listingId}/approve");
        if (accessToken is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(message);
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

    private async Task SetListingStatusAsync(Guid listingId, ListingStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = await dbContext.Listings.SingleAsync(candidate => candidate.Id == listingId);
        listing.Status = status;
        await dbContext.SaveChangesAsync();
    }

    private async Task<Listing> ListingAsync(Guid listingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Listings
            .AsNoTracking()
            .SingleAsync(listing => listing.Id == listingId);
    }

    private async Task<List<ModerationAuditLog>> AuditLogsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.ModerationAuditLogs
            .AsNoTracking()
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();
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
