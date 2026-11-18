using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Reports;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingReportEndpointTests
{
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
        await TestListingsSeed.SeedAsync(dbContext);
        _listingIds = await LoadListingIdsAsync(dbContext, 6);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/listings/{_listingIds[0]}/reports",
            new CreateListingReportRequest { Reason = "spam" },
            ApiJson.Options);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Create_ValidPublishedListing_ReturnsCreatedAndPersists()
    {
        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest
            {
                Reason = "scam",
                Details = "  <b>Deposit is fake</b>\u0007 call me  ",
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<CreateListingReportResponse>(ApiJson.Options);
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(
            response.Headers.Location?.ToString(),
            Is.EqualTo($"/api/v1/moderation/reports/{created.Id}"));

        var reports = await ReportsAsync(_listingIds[0]);
        Assert.That(reports, Has.Count.EqualTo(1));
        var report = reports[0];
        Assert.Multiple(() =>
        {
            Assert.That(report.Id, Is.EqualTo(created.Id));
            Assert.That(report.ReporterId, Is.EqualTo(_userId));
            Assert.That(report.Reason, Is.EqualTo(ListingReportReason.Scam));
            Assert.That(report.Details, Is.EqualTo("Deposit is fake call me"));
            Assert.That(report.Status, Is.EqualTo(ListingReportStatus.Open));
            Assert.That(report.ClaimedByUserId, Is.Null);
            Assert.That(report.Version, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Create_DetailsWithoutText_StoresNull()
    {
        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest { Reason = "spam", Details = " <b> </b> " });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var reports = await ReportsAsync(_listingIds[0]);
        Assert.That(reports[0].Details, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("bogus")]
    [TestCase("wrong-info")]
    public async Task Create_InvalidReason_ReturnsInvalidReasonProblem(string? reason)
    {
        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest { Reason = reason });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_report_reason");
    }

    [Test]
    public async Task Create_DetailsTooLong_ReturnsInvalidDetailsProblem()
    {
        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest
            {
                Reason = "spam",
                Details = new string('a', 1001)
            });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_report_details");
    }

    [Test]
    public async Task Create_RawDetailsTooLong_ReturnsInvalidDetailsProblem()
    {
        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest
            {
                Reason = "spam",
                Details = new string('a', ReportDetailsSanitizer.MaximumInputLength + 1)
            });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_report_details");
    }

    [Test]
    public async Task Create_TagHeavyDetailsUnderRawLimit_AreSanitized()
    {
        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest
            {
                Reason = "spam",
                Details = string.Concat(Enumerable.Repeat("<b>a</b>", 900))
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var reports = await ReportsAsync(_listingIds[0]);
        Assert.That(reports[0].Details, Is.EqualTo(new string('a', 900)));
    }

    [Test]
    public async Task Create_UnknownListing_ReturnsListingNotFound()
    {
        var response = await CreateAsync(
            Guid.NewGuid(),
            await AccessTokenAsync(),
            new CreateListingReportRequest { Reason = "spam" });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "listing_not_found");
    }

    [TestCase(20)] // draft
    [TestCase(22)] // hidden
    [TestCase(24)] // removed
    public async Task Create_NonPublishedListing_ReturnsListingNotFound(int listingIndex)
    {
        var response = await CreateAsync(
            TestListingsSeed.IdFor(listingIndex),
            await AccessTokenAsync(),
            new CreateListingReportRequest { Reason = "spam" });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "listing_not_found");
    }

    [Test]
    public async Task Create_OwnListing_ReturnsUnprocessableEntity()
    {
        await SetOwnerAsync(_listingIds[0], _userId);

        var response = await CreateAsync(
            _listingIds[0],
            await AccessTokenAsync(),
            new CreateListingReportRequest { Reason = "wrong_info" });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "cannot_report_own_listing");
    }

    [Test]
    public async Task Create_SecondOpenReport_ReturnsConflict()
    {
        var token = await AccessTokenAsync();
        var first = await CreateAsync(
            _listingIds[0],
            token,
            new CreateListingReportRequest { Reason = "spam" });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var second = await CreateAsync(
            _listingIds[0],
            token,
            new CreateListingReportRequest { Reason = "scam" });

        await AssertProblemAsync(second, HttpStatusCode.Conflict, "duplicate_open_report");
        Assert.That(await ReportsAsync(_listingIds[0]), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Create_WhilePreviousReportInReview_ReturnsConflict()
    {
        var token = await AccessTokenAsync();
        var first = await CreateAsync(
            _listingIds[0],
            token,
            new CreateListingReportRequest { Reason = "spam" });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var report = await dbContext.ListingReports.SingleAsync();
            report.Status = ListingReportStatus.InReview;
            await dbContext.SaveChangesAsync();
        }

        var second = await CreateAsync(
            _listingIds[0],
            token,
            new CreateListingReportRequest { Reason = "scam" });

        await AssertProblemAsync(second, HttpStatusCode.Conflict, "duplicate_open_report");
        Assert.That(await ReportsAsync(_listingIds[0]), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Create_AfterPreviousReportWasResolved_AllowsNewOpenReport()
    {
        var token = await AccessTokenAsync();
        var first = await CreateAsync(
            _listingIds[0],
            token,
            new CreateListingReportRequest { Reason = "spam" });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var report = await dbContext.ListingReports.SingleAsync();
            report.Status = ListingReportStatus.Resolved;
            await dbContext.SaveChangesAsync();
        }

        var second = await CreateAsync(
            _listingIds[0],
            token,
            new CreateListingReportRequest { Reason = "scam" });

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(await ReportsAsync(_listingIds[0]), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Create_SixthReportWithinAnHour_ReturnsRateLimited()
    {
        var token = await AccessTokenAsync();
        for (var index = 0; index < 5; index++)
        {
            var response = await CreateAsync(
                _listingIds[index],
                token,
                new CreateListingReportRequest { Reason = "spam" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        var limited = await CreateAsync(
            _listingIds[5],
            token,
            new CreateListingReportRequest { Reason = "spam" });

        Assert.Multiple(() =>
        {
            Assert.That(limited.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(limited.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(limited.Headers.RetryAfter, Is.Not.Null);
        });

        using var document = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("report_rate_limited"));
            Assert.That(
                document.RootElement.GetProperty("retry_after_seconds").GetInt32(),
                Is.GreaterThan(0));
        });
        Assert.That(await ReportsAsync(_listingIds[5]), Is.Empty);
    }

    [Test]
    public async Task Create_ReportsOlderThanAnHour_DoNotCountTowardsRateLimit()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var index = 0; index < 5; index++)
            {
                dbContext.ListingReports.Add(new ListingReport
                {
                    Id = Guid.NewGuid(),
                    ListingId = _listingIds[index],
                    ReporterId = _userId,
                    Reason = ListingReportReason.Spam,
                    Status = ListingReportStatus.Resolved,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var response = await CreateAsync(
            _listingIds[5],
            await AccessTokenAsync(),
            new CreateListingReportRequest { Reason = "spam" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
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

    private async Task<HttpResponseMessage> CreateAsync(
        Guid listingId,
        string accessToken,
        CreateListingReportRequest request)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/listings/{listingId}/reports")
        {
            Content = JsonContent.Create(request, options: ApiJson.Options)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(message);
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

    private async Task SetOwnerAsync(Guid listingId, Guid ownerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = await dbContext.Listings.SingleAsync(candidate => candidate.Id == listingId);
        listing.OwnerId = ownerId;
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<ListingReport>> ReportsAsync(Guid listingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.ListingId == listingId)
            .OrderBy(report => report.CreatedAt)
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
