using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.Services.RoomDetails;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingReportServiceTests
{
    [Test]
    public async Task SubmitReportAsync_WhenCreated_ReturnsSuccessWithReportDetails()
    {
        string? capturedPath = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedPath = request.RequestUri?.PathAndQuery;
            capturedBody = await request.Content!.ReadAsStringAsync();
            var payload = JsonSerializer.Serialize(new
            {
                reportId = "rep-123",
                roomId = "room-01",
                status = "Pending",
                createdAtUtc = new DateTimeOffset(2026, 11, 26, 1, 0, 0, TimeSpan.Zero)
            });
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ListingReportService(new ApiClient(httpClient));

        var result = await service.SubmitReportAsync("room-01", ReportReasonCode.SuspectedScam, "  Fake deposit  ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ReportId, Is.EqualTo("rep-123"));
            Assert.That(result.Status, Is.EqualTo("Pending"));
            Assert.That(capturedPath, Is.EqualTo("/api/v1/rooms/room-01/reports"));
            Assert.That(capturedBody, Does.Contain("\"reason\":2"));
            Assert.That(capturedBody, Does.Contain("\"note\":\"Fake deposit\""));
        });
    }

    [Test]
    public async Task SubmitReportAsync_WhenConflict_ReturnsDuplicateFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ListingReportService(new ApiClient(httpClient));

        var result = await service.SubmitReportAsync("room-01", ReportReasonCode.InaccurateInfo, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsDuplicate, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("You have already reported this listing."));
        });
    }

    [Test]
    public async Task SubmitReportAsync_WhenUnauthorized_ReturnsUnauthorizedFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ListingReportService(new ApiClient(httpClient));

        var result = await service.SubmitReportAsync("room-01", ReportReasonCode.InaccurateInfo, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsUnauthorized, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Please sign in again to report this listing."));
        });
    }

    [Test]
    public async Task SubmitReportAsync_WhenTooManyRequests_ReturnsRateLimitedFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ListingReportService(new ApiClient(httpClient));

        var result = await service.SubmitReportAsync("room-01", ReportReasonCode.InaccurateInfo, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsRateLimited, Is.True);
            Assert.That(result.ErrorMessage, Is.EqualTo("Too many reports submitted. Please try again later."));
        });
    }

    [Test]
    public async Task SubmitReportAsync_WhenRoomIdIsBlank_ReturnsValidationFailureWithoutHttpCall()
    {
        var called = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var service = new ListingReportService(new ApiClient(httpClient));

        var result = await service.SubmitReportAsync("   ", ReportReasonCode.InaccurateInfo, null);

        Assert.Multiple(() =>
        {
            Assert.That(called, Is.False);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsValidationError, Is.True);
        });
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request);
    }
}
