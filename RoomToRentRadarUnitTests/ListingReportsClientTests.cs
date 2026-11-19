using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingReportsClientTests
{
    private static readonly Guid ListingId = Guid.Parse("5c6b6c1e-9d54-4bde-9f4b-2a1f0f0a0001");

    [Test]
    public async Task CreateReportAsync_PostsWireReasonAndTrimmedDetails_AndReadsCreatedId()
    {
        var reportId = Guid.NewGuid();
        CreateListingReportRequest? sent = null;
        var handler = new StubHandler(async request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(
                request.RequestUri!.AbsolutePath,
                Is.EqualTo($"/api/v1/listings/{ListingId}/reports"));
            sent = JsonSerializer.Deserialize<CreateListingReportRequest>(
                await request.Content!.ReadAsStringAsync(),
                ApiJson.Options);
            return JsonResponse(HttpStatusCode.Created, new CreateListingReportResponse(reportId));
        });

        var result = await Client(handler).CreateReportAsync(
            ListingId,
            ListingReportReason.WrongInfo,
            "  Giá niêm yết sai  ");

        Assert.Multiple(() =>
        {
            Assert.That(sent?.Reason, Is.EqualTo("wrong_info"));
            Assert.That(sent?.Details, Is.EqualTo("Giá niêm yết sai"));
            Assert.That(result.Outcome, Is.EqualTo(ListingReportSubmitOutcome.Created));
            Assert.That(result.ReportId, Is.EqualTo(reportId));
        });
    }

    [Test]
    public async Task CreateReportAsync_WhenDetailsAreBlank_SendsNullDetails()
    {
        CreateListingReportRequest? sent = null;
        var handler = new StubHandler(async request =>
        {
            sent = JsonSerializer.Deserialize<CreateListingReportRequest>(
                await request.Content!.ReadAsStringAsync(),
                ApiJson.Options);
            return JsonResponse(HttpStatusCode.Created, new CreateListingReportResponse(Guid.NewGuid()));
        });

        await Client(handler).CreateReportAsync(ListingId, ListingReportReason.Spam, "   ");

        Assert.That(sent?.Details, Is.Null);
    }

    [TestCase(HttpStatusCode.Unauthorized, ListingReportSubmitOutcome.Unauthorized)]
    [TestCase(HttpStatusCode.NotFound, ListingReportSubmitOutcome.ListingNotFound)]
    [TestCase(HttpStatusCode.Conflict, ListingReportSubmitOutcome.DuplicateOpenReport)]
    [TestCase(HttpStatusCode.UnprocessableEntity, ListingReportSubmitOutcome.OwnListing)]
    public async Task CreateReportAsync_MapsKnownFailureStatuses(
        HttpStatusCode status,
        ListingReportSubmitOutcome expected)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)));

        var result = await Client(handler).CreateReportAsync(ListingId, ListingReportReason.Scam, null);

        Assert.That(result.Outcome, Is.EqualTo(expected));
    }

    [Test]
    public async Task CreateReportAsync_WhenRateLimited_UsesRetryAfterHeader()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(42));
            return Task.FromResult(response);
        });

        var result = await Client(handler).CreateReportAsync(ListingId, ListingReportReason.Scam, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ListingReportSubmitOutcome.RateLimited));
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(42)));
        });
    }

    [Test]
    public async Task CreateReportAsync_WhenRateLimitedWithoutHeader_ReadsRetrySecondsFromBody()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """{"code":"rate_limited","retry_after_seconds":75}""",
                Encoding.UTF8,
                "application/json")
        }));

        var result = await Client(handler).CreateReportAsync(ListingId, ListingReportReason.Scam, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ListingReportSubmitOutcome.RateLimited));
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(75)));
        });
    }

    [Test]
    public async Task CreateReportAsync_WhenRateLimitedWithoutRetryDetails_FallsBackToOneMinute()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        }));

        var result = await Client(handler).CreateReportAsync(ListingId, ListingReportReason.Scam, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ListingReportSubmitOutcome.RateLimited));
            Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromMinutes(1)));
        });
    }

    [Test]
    public void CreateReportAsync_WhenServerFails_Throws()
    {
        var handler = new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await Client(handler).CreateReportAsync(ListingId, ListingReportReason.Scam, null));
    }

    private static ListingReportsClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode status, T value) => new(status)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(value, ApiJson.Options),
            Encoding.UTF8,
            "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => respond(request);
    }
}
