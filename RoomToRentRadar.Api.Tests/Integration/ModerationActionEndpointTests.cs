using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Moderation;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ModerationActionEndpointTests
{
    private const string ModeratorEmail = "moderator@example.test";
    private const string OtherModeratorEmail = "moderator2@example.test";
    private const string AdminEmail = "admin@example.test";
    private const string Note = "Checked with the landlord.";
    private static readonly Guid ModeratorId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OtherModeratorId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid AdminId = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid Reporter2Id = new("cccccccc-0000-0000-0000-000000000004");
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
            dbContext, ModeratorId, ModeratorEmail, UserRole.Moderator, "Moderator One");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, OtherModeratorId, OtherModeratorEmail, UserRole.Moderator, "Moderator Two");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, AdminId, AdminEmail, UserRole.Admin, "Admin Tester");
        await TestUsersSeed.SeedAdditionalAsync(
            dbContext, Reporter2Id, "reporter2@example.test", displayName: "Reporter Two");
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

    [Test]
    public async Task Actions_AnonymousAndMembersAreRejected()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var anonymousClaim = await ClaimAsync(reportId, accessToken: null);
        var anonymousRelease = await ReleaseAsync(reportId, accessToken: null);
        var anonymousResolve = await ResolveAsync(reportId, null, ModerationResolutionAction.Dismiss, Note);
        var memberClaim = await ClaimAsync(reportId, await AccessTokenAsync(TestUsersSeed.Email));
        var memberRelease = await ReleaseAsync(reportId, await AccessTokenAsync(TestUsersSeed.Email));
        var memberResolve = await ResolveAsync(
            reportId, await AccessTokenAsync(TestUsersSeed.Email), ModerationResolutionAction.Dismiss, Note);

        await AssertProblemAsync(anonymousClaim, HttpStatusCode.Unauthorized, "unauthorized");
        await AssertProblemAsync(anonymousRelease, HttpStatusCode.Unauthorized, "unauthorized");
        await AssertProblemAsync(anonymousResolve, HttpStatusCode.Unauthorized, "unauthorized");
        await AssertProblemAsync(memberClaim, HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(memberRelease, HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(memberResolve, HttpStatusCode.Forbidden, "forbidden");
    }

    [Test]
    public async Task Claim_ClaimsWholeGroupAndWritesAudit()
    {
        var first = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        var second = await SeedReportAsync(_listingId, Reporter2Id, ListingReportStatus.Open);
        var resolved = await SeedReportAsync(
            _listingId, AdminId, ListingReportStatus.Resolved, ListingReportReason.Scam);

        var before = DateTime.UtcNow;
        var response = await ClaimAsync(first, await AccessTokenAsync(ModeratorEmail));
        var after = DateTime.UtcNow;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var reports = await ReportsAsync(_listingId);
        var activeReports = reports.Where(report => report.Id != resolved).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(activeReports, Has.Count.EqualTo(2));
            Assert.That(activeReports.Select(report => report.Id), Is.EquivalentTo(new[] { first, second }));
            Assert.That(activeReports.All(report => report.Status == ListingReportStatus.InReview), Is.True);
            Assert.That(activeReports.All(report => report.ClaimedByUserId == ModeratorId), Is.True);
            Assert.That(activeReports.All(report => report.Version == 1), Is.True);
            Assert.That(activeReports.All(report =>
                report.ClaimedUntilUtc >= before.Add(ModerationActionsService.ClaimDuration)), Is.True);
            Assert.That(activeReports.All(report =>
                report.ClaimedUntilUtc <= after.Add(ModerationActionsService.ClaimDuration)), Is.True);
        });

        var untouched = reports.Single(report => report.Id == resolved);
        Assert.Multiple(() =>
        {
            Assert.That(untouched.Status, Is.EqualTo(ListingReportStatus.Resolved));
            Assert.That(untouched.ClaimedByUserId, Is.Null);
            Assert.That(untouched.Version, Is.EqualTo(0));
        });

        var audit = await AuditLogsAsync();
        Assert.That(audit, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(audit[0].Action, Is.EqualTo(ModerationAuditAction.Claim));
            Assert.That(audit[0].ActorUserId, Is.EqualTo(ModeratorId));
            Assert.That(audit[0].ListingId, Is.EqualTo(_listingId));
            Assert.That(audit[0].ReportId, Is.EqualTo(first));
            Assert.That(audit[0].ResolutionAction, Is.Null);
            Assert.That(audit[0].PreviousReportStatus, Is.Null);
            Assert.That(audit[0].NextReportStatus, Is.Null);
        });
    }

    [Test]
    public async Task Claim_AlreadyClaimedByAnotherModerator_ReturnsConflict()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        Assert.That(
            (await ClaimAsync(reportId, await AccessTokenAsync(ModeratorEmail))).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ClaimAsync(reportId, await AccessTokenAsync(OtherModeratorEmail));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "claim_conflict");
        var report = await ReportAsync(reportId);
        Assert.That(report.ClaimedByUserId, Is.EqualTo(ModeratorId));
    }

    [Test]
    public async Task Claim_ExpiredClaimCanBeTakenOver()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        Assert.That(
            (await ClaimAsync(reportId, await AccessTokenAsync(ModeratorEmail))).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        await SetClaimExpiryAsync(reportId, DateTime.UtcNow.AddMinutes(-1));

        var response = await ClaimAsync(reportId, await AccessTokenAsync(OtherModeratorEmail));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var report = await ReportAsync(reportId);
        Assert.That(report.ClaimedByUserId, Is.EqualTo(OtherModeratorId));
    }

    [Test]
    public async Task Claim_UnknownReport_ReturnsNotFound()
    {
        var response = await ClaimAsync(Guid.NewGuid(), await AccessTokenAsync(ModeratorEmail));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "report_not_found");
    }

    [Test]
    public async Task Release_OwnerClearsClaimKeepsInReviewAndWritesAudit()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        var token = await AccessTokenAsync(ModeratorEmail);
        Assert.That((await ClaimAsync(reportId, token)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ReleaseAsync(reportId, token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var report = await ReportAsync(reportId);
        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ListingReportStatus.InReview));
            Assert.That(report.ClaimedByUserId, Is.Null);
            Assert.That(report.ClaimedUntilUtc, Is.Null);
            Assert.That(report.Version, Is.EqualTo(2));
        });

        var audit = await AuditLogsAsync();
        Assert.That(audit.Select(log => log.Action), Is.EqualTo(
            new[] { ModerationAuditAction.Claim, ModerationAuditAction.Release }));
    }

    [Test]
    public async Task Release_ByAnotherModerator_ReturnsForbidden()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        Assert.That(
            (await ClaimAsync(reportId, await AccessTokenAsync(ModeratorEmail))).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ReleaseAsync(reportId, await AccessTokenAsync(OtherModeratorEmail));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "not_claim_owner");
        Assert.That((await ReportAsync(reportId)).ClaimedByUserId, Is.EqualTo(ModeratorId));
    }

    [Test]
    public async Task Release_ByAdmin_ReleasesAnotherModeratorsClaim()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        Assert.That(
            (await ClaimAsync(reportId, await AccessTokenAsync(ModeratorEmail))).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ReleaseAsync(reportId, await AccessTokenAsync(AdminEmail));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await ReportAsync(reportId)).ClaimedByUserId, Is.Null);
    }

    [Test]
    public async Task Release_WithoutClaim_ReturnsConflict()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var response = await ReleaseAsync(reportId, await AccessTokenAsync(ModeratorEmail));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "claim_conflict");
    }

    [TestCase(ModerationResolutionAction.Dismiss, ListingStatus.Published, ListingReportStatus.Dismissed, 0)]
    [TestCase(ModerationResolutionAction.HideListing, ListingStatus.Hidden, ListingReportStatus.Resolved, 1)]
    [TestCase(ModerationResolutionAction.RemoveListing, ListingStatus.Removed, ListingReportStatus.Resolved, 1)]
    [TestCase(ModerationResolutionAction.WarnOwner, ListingStatus.Published, ListingReportStatus.Resolved, 0)]
    public async Task Resolve_AppliesActionToWholeGroupAndWritesAudit(
        ModerationResolutionAction action,
        ListingStatus expectedListingStatus,
        ListingReportStatus expectedReportStatus,
        long expectedListingVersion)
    {
        var first = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        var second = await SeedReportAsync(_listingId, Reporter2Id, ListingReportStatus.Open);
        var token = await AccessTokenAsync(ModeratorEmail);
        Assert.That((await ClaimAsync(first, token)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ResolveAsync(first, token, action, Note);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var reports = await ReportsAsync(_listingId);
        Assert.Multiple(() =>
        {
            Assert.That(reports, Has.Count.EqualTo(2));
            Assert.That(reports.Select(report => report.Status), Is.EqualTo(
                new[] { expectedReportStatus, expectedReportStatus }));
            Assert.That(reports.All(report => report.ClaimedByUserId is null), Is.True);
            Assert.That(reports.All(report => report.Version == 2), Is.True);
        });

        var listing = await ListingAsync(_listingId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.Status, Is.EqualTo(expectedListingStatus));
            Assert.That(listing.Version, Is.EqualTo(expectedListingVersion));
        });

        var audit = await AuditLogsAsync();
        Assert.That(audit, Has.Count.EqualTo(2));
        var resolveLog = audit.Single(log => log.Action == ModerationAuditAction.Resolve);
        Assert.Multiple(() =>
        {
            Assert.That(resolveLog.ActorUserId, Is.EqualTo(ModeratorId));
            Assert.That(resolveLog.ListingId, Is.EqualTo(_listingId));
            Assert.That(resolveLog.ReportId, Is.EqualTo(first));
            Assert.That(resolveLog.ResolutionAction, Is.EqualTo(action));
            Assert.That(resolveLog.PreviousReportStatus, Is.EqualTo(ListingReportStatus.InReview));
            Assert.That(resolveLog.NextReportStatus, Is.EqualTo(expectedReportStatus));
            Assert.That(resolveLog.Note, Is.EqualTo(Note));
        });
    }

    [Test]
    public async Task Resolve_HideAlreadyHiddenListing_ResolvesWithoutVersionBump()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        await SetListingStatusAsync(_listingId, ListingStatus.Hidden);

        var response = await ResolveAsync(
            reportId, await AccessTokenAsync(AdminEmail), ModerationResolutionAction.HideListing, Note);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var listing = await ListingAsync(_listingId);
        var report = await ReportAsync(reportId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.Status, Is.EqualTo(ListingStatus.Hidden));
            Assert.That(listing.Version, Is.EqualTo(0));
            Assert.That(report.Status, Is.EqualTo(ListingReportStatus.Resolved));
        });
    }

    [Test]
    public async Task Resolve_HideRemovedListing_ReturnsInvalidListingTransition()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        await SetListingStatusAsync(_listingId, ListingStatus.Removed);

        var response = await ResolveAsync(
            reportId, await AccessTokenAsync(AdminEmail), ModerationResolutionAction.HideListing, Note);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_listing_transition");
        var listing = await ListingAsync(_listingId);
        var report = await ReportAsync(reportId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.Status, Is.EqualTo(ListingStatus.Removed));
            Assert.That(listing.Version, Is.EqualTo(0));
            Assert.That(report.Status, Is.EqualTo(ListingReportStatus.Open));
        });
        Assert.That(await AuditLogsAsync(), Is.Empty);
    }

    [Test]
    public async Task Resolve_MixedReportStatuses_WritesNullPreviousStatus()
    {
        var openReport = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        await SeedReportAsync(_listingId, Reporter2Id, ListingReportStatus.InReview);

        var response = await ResolveAsync(
            openReport, await AccessTokenAsync(AdminEmail), ModerationResolutionAction.Dismiss, Note);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var resolveLog = (await AuditLogsAsync())
            .Single(log => log.Action == ModerationAuditAction.Resolve);
        Assert.Multiple(() =>
        {
            Assert.That(resolveLog.PreviousReportStatus, Is.Null);
            Assert.That(resolveLog.NextReportStatus, Is.EqualTo(ListingReportStatus.Dismissed));
        });
    }

    [Test]
    public async Task Resolve_WithoutClaim_ReturnsConflict()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var response = await ResolveAsync(
            reportId, await AccessTokenAsync(ModeratorEmail), ModerationResolutionAction.Dismiss, Note);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "claim_conflict");
        Assert.That((await ReportAsync(reportId)).Status, Is.EqualTo(ListingReportStatus.Open));
    }

    [Test]
    public async Task Resolve_ByAnotherModerator_ReturnsForbidden()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        Assert.That(
            (await ClaimAsync(reportId, await AccessTokenAsync(ModeratorEmail))).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ResolveAsync(
            reportId,
            await AccessTokenAsync(OtherModeratorEmail),
            ModerationResolutionAction.Dismiss,
            Note);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "not_claim_owner");
        Assert.That((await ReportAsync(reportId)).Status, Is.EqualTo(ListingReportStatus.InReview));
    }

    [Test]
    public async Task Resolve_ByAdminWithoutClaim_Succeeds()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var response = await ResolveAsync(
            reportId, await AccessTokenAsync(AdminEmail), ModerationResolutionAction.WarnOwner, Note);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await ReportAsync(reportId)).Status, Is.EqualTo(ListingReportStatus.Resolved));
    }

    [Test]
    public async Task Resolve_SecondTime_ReturnsInvalidReportState()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        var token = await AccessTokenAsync(AdminEmail);
        Assert.That(
            (await ResolveAsync(reportId, token, ModerationResolutionAction.Dismiss, Note)).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        var response = await ResolveAsync(reportId, token, ModerationResolutionAction.Dismiss, Note);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_report_state");
    }

    [Test]
    public async Task Resolve_UnknownReport_ReturnsNotFound()
    {
        var response = await ResolveAsync(
            Guid.NewGuid(), await AccessTokenAsync(AdminEmail), ModerationResolutionAction.Dismiss, Note);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "report_not_found");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("delete_everything")]
    public async Task Resolve_InvalidAction_ReturnsBadRequest(string? action)
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var response = await ResolveRawAsync(
            reportId,
            await AccessTokenAsync(AdminEmail),
            new ResolveModerationReportRequest { Action = action, Note = Note });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_resolution_action");
    }

    [TestCase(null)]
    [TestCase("   ")]
    public async Task Resolve_MissingNote_ReturnsBadRequest(string? note)
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var response = await ResolveRawAsync(
            reportId,
            await AccessTokenAsync(AdminEmail),
            new ResolveModerationReportRequest { Action = "dismiss", Note = note });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_moderation_note");
    }

    [Test]
    public async Task Resolve_NoteTooLong_ReturnsBadRequest()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);

        var response = await ResolveRawAsync(
            reportId,
            await AccessTokenAsync(AdminEmail),
            new ResolveModerationReportRequest
            {
                Action = "dismiss",
                Note = new string('a', 1001)
            });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_moderation_note");
    }

    [Test]
    public async Task Resolve_TwoParallelRequests_OnlyOneSucceeds()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        var token = await AccessTokenAsync(ModeratorEmail);
        Assert.That((await ClaimAsync(reportId, token)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var responses = await Task.WhenAll(
            ResolveAsync(reportId, token, ModerationResolutionAction.Dismiss, Note),
            ResolveAsync(reportId, token, ModerationResolutionAction.HideListing, Note));

        var successes = responses.Count(response => response.StatusCode == HttpStatusCode.NoContent);
        var conflicts = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Multiple(() =>
        {
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(conflicts, Is.EqualTo(1));
        });

        var audit = await AuditLogsAsync();
        Assert.That(audit.Count(log => log.Action == ModerationAuditAction.Resolve), Is.EqualTo(1));
    }

    [Test]
    public async Task Claim_TwoParallelModerators_OnlyOneSucceeds()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        var firstToken = await AccessTokenAsync(ModeratorEmail);
        var secondToken = await AccessTokenAsync(OtherModeratorEmail);

        var responses = await Task.WhenAll(
            ClaimAsync(reportId, firstToken),
            ClaimAsync(reportId, secondToken));

        var successes = responses.Count(response => response.StatusCode == HttpStatusCode.NoContent);
        var conflicts = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Multiple(() =>
        {
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(conflicts, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AuditLog_CannotBeUpdatedOrDeleted()
    {
        var reportId = await SeedReportAsync(_listingId, _userId, ListingReportStatus.Open);
        Assert.That(
            (await ClaimAsync(reportId, await AccessTokenAsync(ModeratorEmail))).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await AuditLogsAsync(), Has.Count.EqualTo(1));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = Assert.ThrowsAsync<SqliteException>(async () =>
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE moderation_audit_logs SET note = 'tampered'"));
        var delete = Assert.ThrowsAsync<SqliteException>(async () =>
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM moderation_audit_logs"));

        Assert.Multiple(() =>
        {
            Assert.That(update!.Message, Does.Contain("append-only"));
            Assert.That(delete!.Message, Does.Contain("append-only"));
        });
        Assert.That(await AuditLogsAsync(), Has.Count.EqualTo(1));
    }

    private async Task<HttpResponseMessage> ClaimAsync(Guid reportId, string? accessToken)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/moderation/reports/{reportId}/claim");
        if (accessToken is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> ReleaseAsync(Guid reportId, string? accessToken)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/moderation/reports/{reportId}/claim");
        if (accessToken is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(message);
    }

    private Task<HttpResponseMessage> ResolveAsync(
        Guid reportId,
        string? accessToken,
        ModerationResolutionAction action,
        string note)
        => ResolveRawAsync(
            reportId,
            accessToken,
            new ResolveModerationReportRequest
            {
                Action = ModerationResolutionActionWire.ToWireValue(action),
                Note = note
            });

    private async Task<HttpResponseMessage> ResolveRawAsync(
        Guid reportId,
        string? accessToken,
        ResolveModerationReportRequest request)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/moderation/reports/{reportId}/resolve")
        {
            Content = JsonContent.Create(request, options: ApiJson.Options)
        };
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

    private async Task<Guid> SeedReportAsync(
        Guid listingId,
        Guid reporterId,
        ListingReportStatus status,
        ListingReportReason reason = ListingReportReason.Spam)
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
            CreatedAt = BaseTime
        };
        dbContext.ListingReports.Add(report);
        await dbContext.SaveChangesAsync();
        return report.Id;
    }

    private async Task<List<ListingReport>> ReportsAsync(Guid listingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.ListingId == listingId)
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .ToListAsync();
    }

    private async Task<ListingReport> ReportAsync(Guid reportId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.ListingReports
            .AsNoTracking()
            .SingleAsync(report => report.Id == reportId);
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

    private async Task SetClaimExpiryAsync(Guid reportId, DateTime claimedUntilUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = await dbContext.ListingReports.SingleAsync(candidate => candidate.Id == reportId);
        report.ClaimedUntilUtc = claimedUntilUtc;
        await dbContext.SaveChangesAsync();
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
