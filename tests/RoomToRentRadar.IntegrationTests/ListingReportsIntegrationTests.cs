using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.IntegrationTests;

public class ListingReportsIntegrationTests
{
    private AuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", "in-memory");
        Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", "IntegrationTestDb");

        _factory = new AuthWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task ListingReports_EndToEnd_SubmitDuplicateRbacAndModerationReview()
    {
        var studentAuth = await RegisterAndLoginAsync("student.reporter@ictu.edu.vn", "STUDENT");
        var landlordAuth = await RegisterAndLoginAsync("landlord.owner@ictu.edu.vn", "LANDLORD");
        var adminToken = await CreateAdminAccessTokenAsync();

        var room = new Room
        {
            Id = "room-report-01",
            Title = "Phòng trọ nghi ngờ sai giá",
            Price = 1500000m,
            Area = 20,
            Address = "Đường Z115",
            LandlordId = landlordAuth.UserId,
            Status = RoomToRentRadar.Domain.Entities.RoomStatus.Available,
            IsVerified = true
        };
        _factory.RoomRepository.Upsert(room);

        var submitPayload = new SubmitListingReportRequest(ReportReason.InaccurateInfo, "Giá thực tế là 2.5 triệu");

        // 1. Anonymous -> 401
        var anonResponse = await _client.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/reports", submitPayload);
        Assert.That(anonResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // 2. Owner landlord self-report -> 400
        var selfReportResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/v1/rooms/{room.Id}/reports",
            landlordAuth.AccessToken,
            submitPayload);
        Assert.That(selfReportResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // 3. Student submits report -> 201 Created
        var studentSubmit = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/v1/rooms/{room.Id}/reports",
            studentAuth.AccessToken,
            submitPayload);
        Assert.That(studentSubmit.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var submittedDto = await studentSubmit.Content.ReadFromJsonAsync<ListingReportSubmittedDto>();
        Assert.Multiple(() =>
        {
            Assert.That(submittedDto, Is.Not.Null);
            Assert.That(submittedDto!.Status, Is.EqualTo(ReportStatus.Pending));
            Assert.That(submittedDto.RoomId, Is.EqualTo(room.Id));
        });

        // 4. Duplicate active report from same student -> 409 Conflict
        var duplicateSubmit = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/v1/rooms/{room.Id}/reports",
            studentAuth.AccessToken,
            submitPayload);
        Assert.That(duplicateSubmit.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        // 5. Student cannot access moderation queue -> 403 Forbidden
        var studentQueue = await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/moderation/reports", studentAuth.AccessToken);
        Assert.That(studentQueue.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // 6. Admin reads moderation queue -> 200 OK with room snapshot
        var adminQueue = await SendAuthorizedAsync(HttpMethod.Get, "/api/v1/moderation/reports?status=Pending", adminToken);
        Assert.That(adminQueue.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var queuePage = await adminQueue.Content.ReadFromJsonAsync<PagedResult<ModerationReportItemDto>>();
        Assert.That(queuePage, Is.Not.Null);
        Assert.That(queuePage!.Items, Has.Count.EqualTo(1));
        var queuedItem = queuePage.Items[0];
        Assert.That(queuedItem.RoomTitleSnapshot, Is.EqualTo(room.Title));

        // 7. Stale ETag review -> 409 Conflict
        var staleReview = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/v1/moderation/reports/{queuedItem.ReportId}",
            adminToken,
            new ReviewListingReportRequest(room.Id, ReportStatus.Resolved, "Đã xử lý", "stale-etag"));
        Assert.That(staleReview.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        // 8. Admin reviews report with current ETag -> 200 OK
        var validReview = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/v1/moderation/reports/{queuedItem.ReportId}",
            adminToken,
            new ReviewListingReportRequest(room.Id, ReportStatus.Resolved, "Đã yêu cầu chủ phòng sửa giá", queuedItem.ETag));
        Assert.That(validReview.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var reviewedDto = await validReview.Content.ReadFromJsonAsync<ModerationReportItemDto>();
        Assert.That(reviewedDto!.Status, Is.EqualTo(ReportStatus.Resolved));
    }

    private async Task<AuthSessionResponse> RegisterAndLoginAsync(string email, string role)
    {
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "StrongPassword123!", role));
        Assert.That(reg.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "StrongPassword123!"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return (await login.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    private async Task<string> CreateAdminAccessTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var adminUser = User.ProvisionAdminUser(
            EmailAddress.Create("admin.moderator@ictu.edu.vn"),
            "hashed-password",
            DateTimeOffset.UtcNow);
        await userRepository.AddAsync(adminUser);
        await unitOfWork.SaveChangesAsync();
        return tokenService.GenerateAccessToken(adminUser, DateTimeOffset.UtcNow).Token;
    }

    private Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
