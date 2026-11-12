using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.IntegrationTests;

public class ProfileIntegrationTests
{
    private AuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
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
    public async Task GetMyProfile_WhenStudentRegistered_ShouldReturnDefaultStudentProfile()
    {
        // 1. Đăng ký & đăng nhập Student
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("profile.test@ictu.edu.vn", "Pass123456", "STUDENT"));
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("profile.test@ictu.edu.vn", "Pass123456"));
        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>();

        // 2. Gọi GET /api/v1/profiles/me
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var profile = await res.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Multiple(() =>
        {
            Assert.That(profile!.Role, Is.EqualTo("STUDENT"));
            Assert.That(profile.Tenant, Is.Not.Null);
            Assert.That(profile.Tenant!.University, Is.EqualTo("ICTU"));
        });
    }

    [Test]
    public async Task UpdateTenantProfile_WhenStudentAccesses_ShouldSucceed()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("tenant.update@ictu.edu.vn", "Pass123456", "STUDENT"));
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("tenant.update@ictu.edu.vn", "Pass123456"));
        var session = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var updateReq = new UpdateTenantProfileRequest(
            FullName: "Nguyễn Văn Sinh Viên",
            PhoneNumber: "0912345678",
            AvatarUrl: "https://avatar.png",
            Bio: "Sinh viên năm 3",
            StudentId: "DTC215123456",
            University: "ICTU",
            Faculty: "CNTT",
            DateOfBirth: new DateOnly(2003, 10, 15),
            Gender: "Male",
            EmergencyContactName: "Mẹ",
            EmergencyContactPhone: "0987654321",
            MaxBudget: 1500000);

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/tenant");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = JsonContent.Create(updateReq);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await res.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.That(updated!.FullName, Is.EqualTo("Nguyễn Văn Sinh Viên"));
        Assert.That(updated.Tenant!.StudentId, Is.EqualTo("DTC215123456"));
    }

    [Test]
    public async Task UpdateLandlordProfile_WhenStudentAttempts_ShouldReturn403Forbidden()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("student.hacker@ictu.edu.vn", "Pass123456", "STUDENT"));
        var session = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("student.hacker@ictu.edu.vn", "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = JsonContent.Create(new UpdateLandlordProfileRequest("Tên", null, null, null, "Trọ ABC", null, null, null, null, null, null, null, null));
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}