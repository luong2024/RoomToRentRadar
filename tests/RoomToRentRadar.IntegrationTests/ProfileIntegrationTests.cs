using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Storage;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

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
    public async Task GetMyProfile_WhenNoJwtProvided_ShouldReturn401Unauthorized()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
    public async Task UpdateTenantProfile_WhenPhoneNumberInvalid_ShouldReturn400BadRequest()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("invalid.phone@ictu.edu.vn", "Pass123456", "STUDENT"));
        var session = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("invalid.phone@ictu.edu.vn", "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var updateReq = new UpdateTenantProfileRequest(
            FullName: "Test Phone",
            PhoneNumber: "invalid-phone-123",
            AvatarUrl: null,
            Bio: null,
            StudentId: null,
            University: "ICTU",
            Faculty: null,
            DateOfBirth: null,
            Gender: "Male",
            EmergencyContactName: null,
            EmergencyContactPhone: null,
            MaxBudget: null);

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/tenant");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = JsonContent.Create(updateReq);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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

    [Test]
    public async Task LandlordProfile_ShouldPersistInCosmosContainerAndMaskSensitiveKycDataOnRead()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("landlord.secure@ictu.edu.vn", "Pass123456", "LANDLORD"));
        var session = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("landlord.secure@ictu.edu.vn", "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var (frontBlob, backBlob) = await RequestKycUploadBlobsAsync(session.AccessToken);

        var updateReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Uy Tín",
            PhoneNumber: "0988776655",
            AvatarUrl: "https://avatar.png/landlord",
            Bio: "Cho thuê phòng trọ khép kín gần trường ICTU",
            BusinessName: "Nhà Trọ Sinh Viên Xanh",
            Address: "Đường Z115, Quyết Thắng, TP. Thái Nguyên",
            CitizenIdNumber: "001202009999",
            CitizenIdIssueDate: new DateOnly(2021, 6, 15),
            IdentityFrontBlobName: frontBlob,
            IdentityBackBlobName: backBlob,
            BankName: "Vietcombank",
            BankAccountNumber: "0123456789",
            BankAccountHolder: "CHU TRO UY TIN");

        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        putReq.Content = JsonContent.Create(updateReq);
        var putRes = await _client.SendAsync(putReq);

        Assert.That(putRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 1. Kiểm tra dữ liệu được lưu thật vào Cosmos DB UserProfiles container (P0)
        var cosmosDoc = await _factory.CosmosDb.UserProfiles.ReadItemAsync(session.UserId.ToString(), session.UserId.ToString());
        Assert.That(cosmosDoc, Is.Not.Null);
        Assert.That(cosmosDoc!.Landlord, Is.Not.Null);
        Assert.That(cosmosDoc.Landlord!.CitizenIdNumber, Is.EqualTo("001202009999"));
        Assert.That(cosmosDoc.Landlord.BankAccountNumber, Is.EqualTo("0123456789"));
        var expectedVerifiedBlob = frontBlob.StartsWith("staging/", StringComparison.Ordinal)
            ? frontBlob.Replace("staging/", "verified/", StringComparison.Ordinal)
            : frontBlob.Replace("/kyc/staging/", "/kyc/verified/", StringComparison.Ordinal);
        Assert.That(cosmosDoc.Landlord.IdentityFrontBlobName, Is.EqualTo(expectedVerifiedBlob));

        // 2. Kiểm tra khi đọc hồ sơ cá nhân qua GET /me:
        // - CCCD & Tài khoản được che giấu (SensitiveDataMasker)
        // - URL ảnh CCCD được cấp chữ ký số SAS có thời hạn thật sự (sp=r, se=, sig=)
        using var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var getRes = await _client.SendAsync(getReq);
        var profile = await getRes.Content.ReadFromJsonAsync<UserProfileDto>();

        Assert.Multiple(() =>
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Landlord, Is.Not.Null);
            Assert.That(profile.Landlord!.CitizenIdNumber, Is.EqualTo("********9999"));
            Assert.That(profile.Landlord.BankAccountNumber, Is.EqualTo("******6789"));
            Assert.That(profile.Landlord.IdentityFrontPhotoUrl, Does.Contain("sp=r").And.Contain("sig="));
            Assert.That(profile.Landlord.IdentityBackPhotoUrl, Does.Contain("sp=r").And.Contain("sig="));
        });
    }

    [Test]
    public async Task GetPublicProfile_ShouldNeverExposeKycDocumentUrlsOrTokens()
    {
        // 1. Landlord nộp hồ sơ KYC
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("landlord.publictest@ictu.edu.vn", "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("landlord.publictest@ictu.edu.vn", "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var (pubFrontBlob, pubBackBlob) = await RequestKycUploadBlobsAsync(landlordSession.AccessToken);

        var updateReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Công Khai",
            PhoneNumber: "0977889900",
            AvatarUrl: "https://avatar.png/public-landlord",
            Bio: "Phòng trọ giá rẻ",
            BusinessName: "Trọ Bình Dân",
            Address: "Gần ICTU",
            CitizenIdNumber: "001202008888",
            CitizenIdIssueDate: new DateOnly(2020, 1, 1),
            IdentityFrontBlobName: pubFrontBlob,
            IdentityBackBlobName: pubBackBlob,
            BankName: "BIDV",
            BankAccountNumber: "9876543210",
            BankAccountHolder: "CHU TRO CONG KHAI");

        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        putReq.Content = JsonContent.Create(updateReq);
        await _client.SendAsync(putReq);

        // 2. Student đăng nhập và xem hồ sơ công khai của Landlord
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("student.viewer@ictu.edu.vn", "Pass123456", "STUDENT"));
        var studentSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest("student.viewer@ictu.edu.vn", "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var getPubReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/profiles/{landlordSession.UserId}/public");
        getPubReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentSession.AccessToken);
        var pubRes = await _client.SendAsync(getPubReq);

        Assert.That(pubRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rawBody = await pubRes.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rawBody, Does.Not.Contain("001202008888"));
            Assert.That(rawBody, Does.Not.Contain("9876543210"));
            Assert.That(rawBody, Does.Not.Contain("secret_front.jpg"));
            Assert.That(rawBody, Does.Not.Contain("secret_back.jpg"));
            Assert.That(rawBody, Does.Not.Contain("sig="));
        });
    }

    [Test]
    public async Task UpdateAdminProfile_WhenAdminAccesses_ShouldSucceedAndPersistInCosmos()
    {
        await ProvisionAdminInDatabaseAsync("admin.profile@ictu.edu.vn", "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "admin.profile@ictu.edu.vn",
            "AdminPass123"))).Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var updateReq = new UpdateAdminProfileRequest(
            FullName: "Quản Trị Viên Hệ Thống",
            PhoneNumber: "0911223344",
            StaffCode: "STAFF-999",
            Department: "Trung tâm Đảm bảo An ninh Thông tin",
            Title: "Trưởng ban Quản trị");

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/admin");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        req.Content = JsonContent.Create(updateReq);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var profile = await res.Content.ReadFromJsonAsync<UserProfileDto>();

        Assert.Multiple(() =>
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Role, Is.EqualTo("ADMIN"));
            Assert.That(profile.Admin, Is.Not.Null);
            Assert.That(profile.Admin!.StaffCode, Is.EqualTo("STAFF-999"));
            Assert.That(profile.Admin.Department, Is.EqualTo("Trung tâm Đảm bảo An ninh Thông tin"));
        });

        // Kiểm tra lưu trữ thật trong Cosmos container
        var cosmosDoc = await _factory.CosmosDb.UserProfiles.ReadItemAsync(adminSession.UserId.ToString(), adminSession.UserId.ToString());
        Assert.That(cosmosDoc, Is.Not.Null);
        Assert.That(cosmosDoc!.Admin, Is.Not.Null);
        Assert.That(cosmosDoc.Admin!.StaffCode, Is.EqualTo("STAFF-999"));
    }

    [Test]
    public async Task UpdateAdminProfile_WhenStudentAttempts_ShouldReturn403Forbidden()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("student.tryadmin@ictu.edu.vn", "Pass123456", "STUDENT"));
        var studentSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "student.tryadmin@ictu.edu.vn",
            "Pass123456"))).Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var updateReq = new UpdateAdminProfileRequest("Fake Admin", null, "FAKE-01", "IT", "Hacker");

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/admin");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentSession.AccessToken);
        req.Content = JsonContent.Create(updateReq);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateAdminProfile_WhenAdminIsInactive_ShouldReturn401Unauthorized()
    {
        await ProvisionAdminInDatabaseAsync("inactive.admin@ictu.edu.vn", "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "inactive.admin@ictu.edu.vn",
            "AdminPass123"))).Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // Vô hiệu hóa tài khoản admin
        await DeactivateUserInDatabaseAsync(adminSession.UserId);

        var updateReq = new UpdateAdminProfileRequest("Inactive Admin", null, "INACTIVE-01", "Security", "Admin");

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/admin");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        req.Content = JsonContent.Create(updateReq);
        var res = await _client.SendAsync(req);

        // Security stamp xoay vòng và tài khoản inactive -> lập tức bị từ chối 401
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetMyProfile_WhenTokenHasMalformedUserId_ShouldReturn401UnauthorizedInsteadOf500()
    {
        // Tạo JWT hợp lệ về signature nhưng claim "sub" không phải là GUID hợp lệ
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthWebApplicationFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", "not-a-guid"),
            new Claim(ClaimTypes.Role, "STUDENT"),
            new Claim(AuthClaimTypes.SecurityStamp, "valid-stamp")
        };

        var token = new JwtSecurityToken(
            issuer: AuthWebApplicationFactory.TestIssuer,
            audience: AuthWebApplicationFactory.TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        var res = await _client.SendAsync(req);

        // Phải trả về 401 Unauthorized theo chuẩn RFC 7807 ProblemDetails thay vì 500
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetPendingLandlords_WhenPaginationQueried_ShouldBeOrderedByUpdatedAtDescAndIdAsc()
    {
        // Tạo 3 landlord nộp hồ sơ cách nhau
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        var t1 = DateTimeOffset.UtcNow.AddMinutes(-30);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-20);
        var t3 = DateTimeOffset.UtcNow.AddMinutes(-10);

        var p1 = UserProfile.CreateDefault(id1, UserRole.Landlord, t1);
        p1.SubmitKycForVerification("001000000001", new DateOnly(2020, 1, 1), "front1", "back1", t1);

        var p2 = UserProfile.CreateDefault(id2, UserRole.Landlord, t2);
        p2.SubmitKycForVerification("001000000002", new DateOnly(2020, 1, 1), "front2", "back2", t2);

        var p3 = UserProfile.CreateDefault(id3, UserRole.Landlord, t3);
        p3.SubmitKycForVerification("001000000003", new DateOnly(2020, 1, 1), "front3", "back3", t3);

        await repo.AddAsync(p1);
        await repo.AddAsync(p2);
        await repo.AddAsync(p3);

        var pendingList = await repo.GetPendingLandlordsAsync(skip: 0, take: 10);

        Assert.That(pendingList, Has.Count.GreaterThanOrEqualTo(3));
        // Đảm bảo thứ tự giảm dần theo UpdatedAt (p3 mới nhất đứng đầu)
        var matching = pendingList.Where(p => p.UserId == id1 || p.UserId == id2 || p.UserId == id3).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(matching[0].UserId, Is.EqualTo(id3));
            Assert.That(matching[1].UserId, Is.EqualTo(id2));
            Assert.That(matching[2].UserId, Is.EqualTo(id1));
        });
    }

    [Test]
    public async Task Admin_GetPendingLandlords_WhenAdminAccesses_ShouldReturnPendingListWithSignedUrls()
    {
        var email = $"landlord.pending.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var (pendingFrontBlob, pendingBackBlob) = await RequestKycUploadBlobsAsync(landlordSession.AccessToken);

        var submitReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Chờ Duyệt",
            PhoneNumber: "0966554433",
            AvatarUrl: "https://avatar.png/pending",
            Bio: "Chờ xét duyệt KYC",
            BusinessName: "Trọ Xanh Pending",
            Address: "Thái Nguyên",
            CitizenIdNumber: "001202007777",
            CitizenIdIssueDate: new DateOnly(2022, 3, 10),
            IdentityFrontBlobName: pendingFrontBlob,
            IdentityBackBlobName: pendingBackBlob,
            BankName: "MBBank",
            BankAccountNumber: "1122334455",
            BankAccountHolder: "CHU TRO CHO DUYET");

        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        putReq.Content = JsonContent.Create(submitReq);
        var putRes = await _client.SendAsync(putReq);
        Assert.That(putRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var adminEmail = $"admin.kyc.{Guid.NewGuid():N}@ictu.edu.vn";
        await ProvisionAdminInDatabaseAsync(adminEmail, "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(adminEmail, "AdminPass123")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var getPendingReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/admin/landlords/pending?skip=0&take=10");
        getPendingReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var getRes = await _client.SendAsync(getPendingReq);

        Assert.That(getRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var list = await getRes.Content.ReadFromJsonAsync<List<AdminLandlordKycDto>>();
        Assert.That(list, Is.Not.Null);

        var item = list!.FirstOrDefault(x => x.UserId == landlordSession.UserId);
        Assert.That(item, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(item!.CitizenIdNumber, Is.EqualTo("001202007777"), "Admin must see unmasked CitizenIdNumber for KYC audit");
            Assert.That(item.VerificationStatus, Is.EqualTo("Pending"));
            Assert.That(item.IdentityFrontPhotoUrl, Does.Contain("sp=r").And.Contain("sig="));
            Assert.That(item.IdentityBackPhotoUrl, Does.Contain("sp=r").And.Contain("sig="));
        });
    }

    [TestCase(-1, 20)]
    [TestCase(0, 0)]
    [TestCase(0, 101)]
    public async Task Admin_GetPendingLandlords_WhenPaginationParametersAreInvalid_ShouldReturnBadRequest(int skip, int take)
    {
        var adminEmail = $"admin.page.{Guid.NewGuid():N}@ictu.edu.vn";
        await ProvisionAdminInDatabaseAsync(adminEmail, "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(adminEmail, "AdminPass123")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/profiles/admin/landlords/pending?skip={skip}&take={take}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var getRes = await _client.SendAsync(getReq);

        Assert.That(getRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Landlord_CreateKycUploadUrl_WhenExceedingRateLimit_ShouldReturn429TooManyRequests()
    {
        var email = $"landlord.ratelimit.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // 10 requests should succeed
        for (var i = 0; i < 10; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/profiles/me/landlord/kyc-upload-url");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
            req.Content = JsonContent.Create(new CreateKycUploadUrlRequest("front", "image/jpeg"));
            var res = await _client.SendAsync(req);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // 11th request must be rejected with 429 Too Many Requests
        using var rateLimitedReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/profiles/me/landlord/kyc-upload-url");
        rateLimitedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        rateLimitedReq.Content = JsonContent.Create(new CreateKycUploadUrlRequest("front", "image/jpeg"));
        var rateLimitedRes = await _client.SendAsync(rateLimitedReq);

        Assert.That(rateLimitedRes.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task Admin_GetLandlordKycDetail_WhenAdminAccesses_ShouldReturnDetailWithSignedSasUrls()
    {
        var email = $"landlord.detail.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var (detailFrontBlob, detailBackBlob) = await RequestKycUploadBlobsAsync(landlordSession.AccessToken);

        var submitReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Chi Tiết",
            PhoneNumber: "0966554422",
            AvatarUrl: "https://avatar.png/detail",
            Bio: "Chi tiết KYC",
            BusinessName: "Trọ Xanh Detail",
            Address: "Thái Nguyên",
            CitizenIdNumber: "001202006666",
            CitizenIdIssueDate: new DateOnly(2022, 4, 15),
            IdentityFrontBlobName: detailFrontBlob,
            IdentityBackBlobName: detailBackBlob,
            BankName: "Techcombank",
            BankAccountNumber: "9988776655",
            BankAccountHolder: "CHU TRO CHI TIET");

        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        putReq.Content = JsonContent.Create(submitReq);
        await _client.SendAsync(putReq);

        var adminEmail = $"admin.detail.{Guid.NewGuid():N}@ictu.edu.vn";
        await ProvisionAdminInDatabaseAsync(adminEmail, "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(adminEmail, "AdminPass123")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var getDetailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/profiles/admin/landlords/{landlordSession.UserId}/kyc");
        getDetailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var getRes = await _client.SendAsync(getDetailReq);

        Assert.That(getRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var detail = await getRes.Content.ReadFromJsonAsync<AdminLandlordKycDto>();
        Assert.That(detail, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(detail!.CitizenIdNumber, Is.EqualTo("001202006666"));
            Assert.That(detail.IdentityFrontPhotoUrl, Does.Contain("sp=r").And.Contain("sig="));
            Assert.That(detail.IdentityBackPhotoUrl, Does.Contain("sp=r").And.Contain("sig="));
        });
    }

    [Test]
    public async Task Admin_GetLandlordKycDetail_WhenStudentAccesses_ShouldReturn403Forbidden()
    {
        var email = $"student.forbidden.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "STUDENT"));
        var studentSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/profiles/admin/landlords/{Guid.NewGuid()}/kyc");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentSession.AccessToken);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_GetPendingLandlords_WhenStudentAccesses_ShouldReturn403Forbidden()
    {
        var email = $"student.pending.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "STUDENT"));
        var studentSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/admin/landlords/pending");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentSession.AccessToken);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_VerifyLandlord_WhenLandlordNotPending_ShouldReturn400BadRequest()
    {
        var email = $"landlord.unverified.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // Khởi tạo hồ sơ Landlord mặc định (trạng thái ban đầu là Unverified)
        using var initProfileReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        initProfileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        var initRes = await _client.SendAsync(initProfileReq);
        Assert.That(initRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var adminEmail = $"admin.verify.{Guid.NewGuid():N}@ictu.edu.vn";
        await ProvisionAdminInDatabaseAsync(adminEmail, "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(adminEmail, "AdminPass123")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var verifyReq = new VerifyLandlordKycRequest(IsApproved: true, Reason: "Quick approve");
        using var postReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/profiles/admin/landlords/{landlordSession.UserId}/verify");
        postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        postReq.Content = JsonContent.Create(verifyReq);
        var postRes = await _client.SendAsync(postReq);

        Assert.That(postRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UpdateLandlord_WhenSubmittingIncompleteKyc_ShouldReturn400BadRequest()
    {
        var email = $"landlord.partial.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var partialKycReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Thiếu Hồ Sơ",
            PhoneNumber: "0966554411",
            AvatarUrl: null,
            Bio: null,
            BusinessName: "Trọ Thiếu Hồ Sơ",
            Address: "Thái Nguyên",
            CitizenIdNumber: "001202005555",
            CitizenIdIssueDate: null,
            IdentityFrontBlobName: $"{landlordSession.UserId:D}/kyc/front_only.jpg",
            IdentityBackBlobName: null,
            BankName: null,
            BankAccountNumber: null,
            BankAccountHolder: null);

        using var putReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        putReq.Content = JsonContent.Create(partialKycReq);
        var res = await _client.SendAsync(putReq);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UpdateLandlord_WhenVerifiedAndChangesKyc_ShouldRevertToPending()
    {
        var email = $"landlord.tamper.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var (origFront, origBack) = await RequestKycUploadBlobsAsync(landlordSession.AccessToken);

        var submitReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Chính Chủ",
            PhoneNumber: "0966554400",
            AvatarUrl: "https://avatar.png/original",
            Bio: "Chủ trọ",
            BusinessName: "Trọ Original",
            Address: "Thái Nguyên",
            CitizenIdNumber: "001202004444",
            CitizenIdIssueDate: new DateOnly(2021, 1, 1),
            IdentityFrontBlobName: origFront,
            IdentityBackBlobName: origBack,
            BankName: "ACB",
            BankAccountNumber: "12345678",
            BankAccountHolder: "CHU TRO CHINH CHU");

        using var putReq1 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        putReq1.Content = JsonContent.Create(submitReq);
        await _client.SendAsync(putReq1);

        var adminEmail = $"admin.tamper.{Guid.NewGuid():N}@ictu.edu.vn";
        await ProvisionAdminInDatabaseAsync(adminEmail, "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(adminEmail, "AdminPass123")))
            .Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var verifyReq = new VerifyLandlordKycRequest(IsApproved: true, Reason: "Approved documents");
        using var postReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/profiles/admin/landlords/{landlordSession.UserId}/verify");
        postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        postReq.Content = JsonContent.Create(verifyReq);
        var postRes = await _client.SendAsync(postReq);
        Assert.That(postRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var verifiedProfile = await postRes.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.That(verifiedProfile!.Landlord!.VerificationStatus, Is.EqualTo("Verified"));

        // Landlord cập nhật CCCD & ảnh mới
        var (newFront, newBack) = await RequestKycUploadBlobsAsync(landlordSession.AccessToken);
        var alteredReq = submitReq with
        {
            CitizenIdNumber = "001202003333",
            IdentityFrontBlobName = newFront,
            IdentityBackBlobName = newBack
        };

        using var putReq2 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        putReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordSession.AccessToken);
        putReq2.Content = JsonContent.Create(alteredReq);
        var putRes2 = await _client.SendAsync(putReq2);

        Assert.That(putRes2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updatedProfile = await putRes2.Content.ReadFromJsonAsync<UserProfileDto>();

        Assert.Multiple(() =>
        {
            Assert.That(updatedProfile!.Landlord!.VerificationStatus, Is.EqualTo("Pending"));
            Assert.That(updatedProfile.Landlord.VerifiedAt, Is.Null);
        });
    }

    [Test]
    public async Task GetMyProfile_WhenMultipleRequestsArriveConcurrently_ShouldNotFailWith409AndReturnProfile()
    {
        // 1. Đăng ký & đăng nhập user mới (chưa có UserProfile document nào trong DB)
        var email = $"concurrent.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "STUDENT"));
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456"));
        var session = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // 2. Bắn 10 request GET /api/v1/profiles/me đồng thời
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            return await _client.SendAsync(req);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        // 3. Toàn bộ 10 request phải thành công với HTTP 200 (không được dính Cosmos 409 hoặc 500)
        foreach (var res in responses)
        {
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var profile = await res.Content.ReadFromJsonAsync<UserProfileDto>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Role, Is.EqualTo("STUDENT"));
            Assert.That(profile.UserId, Is.EqualTo(session.UserId));
        }
    }

    [Test]
    public async Task GetMyProfile_WhenTokenHasNoRoleClaim_ShouldReturn401Unauthorized()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthWebApplicationFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(AuthClaimTypes.SecurityStamp, "valid-stamp")
        };

        var token = new JwtSecurityToken(
            issuer: AuthWebApplicationFactory.TestIssuer,
            audience: AuthWebApplicationFactory.TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetMyProfile_WhenTokenHasInvalidRoleClaim_ShouldReturn401Unauthorized()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthWebApplicationFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "SUPER_ADMIN_HACKER"),
            new Claim(AuthClaimTypes.SecurityStamp, "valid-stamp")
        };

        var token = new JwtSecurityToken(
            issuer: AuthWebApplicationFactory.TestIssuer,
            audience: AuthWebApplicationFactory.TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Admin_UpdateAdminProfile_ShouldPreserveExistingAvatarAndBioWhenNotProvided()
    {
        var adminEmail = $"admin.preserve.{Guid.NewGuid():N}@ictu.edu.vn";
        await ProvisionAdminInDatabaseAsync(adminEmail, "AdminPass1234");
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(adminEmail, "AdminPass1234"));
        var adminSession = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // 1. Cập nhật hồ sơ admin ban đầu kèm AvatarUrl và Bio
        var initialReq = new UpdateAdminProfileRequest(
            FullName: "Admin Ban Đầu",
            PhoneNumber: "0911222333",
            StaffCode: "NV-001",
            Department: "Công nghệ thông tin",
            Title: "Trưởng phòng",
            AvatarUrl: "https://storage.roomtorentradar.vn/avatars/admin_orig.png",
            Bio: "Mô tả ban đầu của quản trị viên.");

        using var req1 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/admin");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        req1.Content = JsonContent.Create(initialReq);
        var res1 = await _client.SendAsync(req1);
        Assert.That(res1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 2. Cập nhật lần 2 chỉ đổi tên và chức danh, KHÔNG truyền AvatarUrl và Bio (null)
        var secondReq = new UpdateAdminProfileRequest(
            FullName: "Admin Đổi Tên",
            PhoneNumber: "0911222444",
            StaffCode: "NV-001",
            Department: "Công nghệ thông tin",
            Title: "Giám đốc kỹ thuật",
            AvatarUrl: null,
            Bio: null);

        using var req2 = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/admin");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        req2.Content = JsonContent.Create(secondReq);
        var res2 = await _client.SendAsync(req2);
        Assert.That(res2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await res2.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Multiple(() =>
        {
            Assert.That(updated!.FullName, Is.EqualTo("Admin Đổi Tên"));
            Assert.That(updated.PhoneNumber, Is.EqualTo("0911222444"));
            Assert.That(updated.Admin!.Title, Is.EqualTo("Giám đốc kỹ thuật"));
            Assert.That(updated.AvatarUrl, Is.EqualTo("https://storage.roomtorentradar.vn/avatars/admin_orig.png"),
                "AvatarUrl cũ phải được giữ nguyên khi request không cập nhật");
            Assert.That(updated.Bio, Is.EqualTo("Mô tả ban đầu của quản trị viên."),
                "Bio cũ phải được giữ nguyên khi request không cập nhật");
        });

        // 3. GET /me cũng phải trả về đúng dữ liệu được bảo toàn
        using var getMeReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        getMeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var getMeRes = await _client.SendAsync(getMeReq);
        var meProfile = await getMeRes.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Multiple(() =>
        {
            Assert.That(meProfile!.AvatarUrl, Is.EqualTo("https://storage.roomtorentradar.vn/avatars/admin_orig.png"));
            Assert.That(meProfile.Bio, Is.EqualTo("Mô tả ban đầu của quản trị viên."));
        });
    }

    [Test]
    public async Task UpdateLandlord_WhenSubmittingKycWithUnauthorizedStorageHost_ShouldReturn400BadRequest()
    {
        var email = $"landlord.badhost.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456"));
        var session = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var invalidHostReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Test",
            PhoneNumber: "0912345678",
            AvatarUrl: null,
            Bio: "Chờ duyệt",
            BusinessName: "Trọ X",
            Address: "Thái Nguyên",
            CitizenIdNumber: "001202008888",
            CitizenIdIssueDate: new DateOnly(2021, 5, 20),
            IdentityFrontBlobName: "https://evil-storage-attacker.com/kyc/front.jpg",
            IdentityBackBlobName: "https://storage.roomtorentradar.vn/kyc/back.jpg",
            BankName: "MB",
            BankAccountNumber: "0987654321",
            BankAccountHolder: "CHU TRO TEST");

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = JsonContent.Create(invalidHostReq);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UpdateLandlord_WhenSubmittingKycWithBlobBelongingToAnotherUser_ShouldReturn400BadRequest()
    {
        var email = $"landlord.badowner.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456"));
        var session = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var victimUserId = Guid.NewGuid();
        var invalidOwnerReq = new UpdateLandlordProfileRequest(
            FullName: "Chủ Trọ Test 2",
            PhoneNumber: "0912345678",
            AvatarUrl: null,
            Bio: "Chờ duyệt",
            BusinessName: "Trọ Y",
            Address: "Thái Nguyên",
            CitizenIdNumber: "001202007777",
            CitizenIdIssueDate: new DateOnly(2021, 5, 20),
            IdentityFrontBlobName: $"{victimUserId:D}/kyc/stolen_front.jpg",
            IdentityBackBlobName: $"{session.UserId:D}/kyc/valid_back.jpg",
            BankName: "MB",
            BankAccountNumber: "0987654321",
            BankAccountHolder: "CHU TRO TEST 2");

        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = JsonContent.Create(invalidOwnerReq);
        var res = await _client.SendAsync(req);

        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LandlordProfile_WhenConcurrentUpdatesOccur_ShouldEncounterOptimisticConcurrencyConflict()
    {
        // 1. Tạo landlord và khởi tạo profile ban đầu
        var email = $"landlord.occ.{Guid.NewGuid():N}@ictu.edu.vn";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "Pass123456", "LANDLORD"));
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "Pass123456"));
        var session = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        using var initReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profiles/me");
        initReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        await _client.SendAsync(initReq);

        // 2. Đồng bộ thời điểm đọc bằng Barrier để đảm bảo cả hai request cùng đọc một phiên bản cũ (ETag) trước khi ghi
        var inMemoryProfiles = (InMemoryCosmosContainer<UserProfileDocument>)_factory.CosmosDb.UserProfiles;
        var barrier = new Barrier(2);
        inMemoryProfiles.OnReadAsync = id =>
        {
            if (id == session.UserId.ToString())
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            }
            return Task.CompletedTask;
        };

        try
        {
            var req1 = new UpdateLandlordProfileRequest("Landlord A", "0911111111", null, "Bio A", "Trọ A", "Hà Nội", null, null, null, null, null, null, null);
            var req2 = new UpdateLandlordProfileRequest("Landlord B", "0922222222", null, "Bio B", "Trọ B", "Thái Nguyên", null, null, null, null, null, null, null);

            var task1 = Task.Run(async () =>
            {
                using var r = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
                r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                r.Content = JsonContent.Create(req1);
                return await _client.SendAsync(r);
            });

            var task2 = Task.Run(async () =>
            {
                using var r = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/landlord");
                r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                r.Content = JsonContent.Create(req2);
                return await _client.SendAsync(r);
            });

            var responses = await Task.WhenAll(task1, task2);

            Assert.Multiple(() =>
            {
                Assert.That(responses.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1), "Exactly one request must succeed with 200 OK");
                Assert.That(responses.Count(r => r.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(1), "The concurrent stale request must be rejected with 409 Conflict");
            });
        }
        finally
        {
            inMemoryProfiles.OnReadAsync = null;
        }
    }

    private async Task<(string FrontBlob, string BackBlob)> RequestKycUploadBlobsAsync(string token)
    {
        using var frontReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/profiles/me/landlord/kyc-upload-url");
        frontReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        frontReq.Content = JsonContent.Create(new CreateKycUploadUrlRequest("front", "image/jpeg"));
        var frontRes = await _client.SendAsync(frontReq);
        var frontUpload = (await frontRes.Content.ReadFromJsonAsync<KycUploadUrlResponse>())!;

        using var backReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/profiles/me/landlord/kyc-upload-url");
        backReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        backReq.Content = JsonContent.Create(new CreateKycUploadUrlRequest("back", "image/jpeg"));
        var backRes = await _client.SendAsync(backReq);
        var backUpload = (await backRes.Content.ReadFromJsonAsync<KycUploadUrlResponse>())!;

        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IKycDocumentStorageService>() as KycDocumentStorageService
            ?? throw new InvalidOperationException("Integration tests require KycDocumentStorageService.");
        storage.RegisterSimulatedBlob(frontUpload.BlobName, "image/jpeg", 1024 * 1024);
        storage.RegisterSimulatedBlob(backUpload.BlobName, "image/jpeg", 1024 * 1024);

        return (frontUpload.BlobName, backUpload.BlobName);
    }

    private async Task ProvisionAdminInDatabaseAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var admin = User.ProvisionAdminUser(
            EmailAddress.Create(email),
            passwordHasher.HashPassword(password),
            DateTimeOffset.UtcNow);

        await userRepository.AddAsync(admin);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task DeactivateUserInDatabaseAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = await userRepository.GetByIdAsync(userId);
        user!.Deactivate(DateTimeOffset.UtcNow);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync();
    }
}
