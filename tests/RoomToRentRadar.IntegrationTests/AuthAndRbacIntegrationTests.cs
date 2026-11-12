using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.IntegrationTests;

public class AuthAndRbacIntegrationTests
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
    public async Task RegisterAndLoginWithEmail_ShouldPersistUserAndReturnTokensWithoutLeakingPasswordHash()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "Student.User@ICTU.edu.vn",
            "SecurePass123",
            "STUDENT"));

        var rawBody = await registerResponse.Content.ReadAsStringAsync();
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterUserResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(registered, Is.Not.Null);
            Assert.That(registered!.Email, Is.EqualTo("student.user@ictu.edu.vn"));
            Assert.That(registered.Role, Is.EqualTo("STUDENT"));
            Assert.That(rawBody, Does.Not.Contain("PasswordHash").IgnoreCase);
            Assert.That(rawBody, Does.Not.Contain("SecurePass123"));
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "student.user@ictu.edu.vn",
            "SecurePass123"));

        var session = await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.UserId, Is.EqualTo(registered!.UserId));
            Assert.That(session.Role, Is.EqualTo("STUDENT"));
            Assert.That(session.AccessToken, Is.Not.Empty);
            Assert.That(session.RefreshToken, Is.Not.Empty);
        });
    }

    [Test]
    public async Task Register_WhenDuplicateEmail_ShouldReturn409ConflictWithProblemDetails()
    {
        var first = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "dup@example.com",
            "SecurePass123",
            "STUDENT"));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var duplicate = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "DUP@example.com",
            "AnotherPass123",
            "LANDLORD"));

        var problem = await duplicate.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(409));
        });
    }

    [Test]
    public async Task Register_WhenClientRequestsAdminRole_ShouldReturn400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "escalate@example.com",
            "SecurePass123",
            "ADMIN"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GoogleAuth_ShouldRegisterNewUserAndPreventConflictWithLocalAccount()
    {
        _factory.GoogleVerifier.RegisterValidToken(
            "google-token-landlord",
            "google-sub-landlord-1",
            "landlord.google@gmail.com");

        var googleResponse = await _client.PostAsJsonAsync("/api/v1/auth/google", new LoginWithGoogleRequest(
            "google-token-landlord",
            "LANDLORD"));

        var session = await googleResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(googleResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Email, Is.EqualTo("landlord.google@gmail.com"));
            Assert.That(session.Role, Is.EqualTo("LANDLORD"));
        });

        // Register a local account and verify Google login with same email is rejected with 409
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "local.only@example.com",
            "SecurePass123",
            "STUDENT"));

        _factory.GoogleVerifier.RegisterValidToken(
            "google-token-conflict",
            "google-sub-conflict-99",
            "local.only@example.com");

        var conflictResponse = await _client.PostAsJsonAsync("/api/v1/auth/google", new LoginWithGoogleRequest(
            "google-token-conflict",
            "STUDENT"));

        Assert.That(conflictResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task RefreshAndLogout_ShouldRotateSingleUseTokensDetectReplayAndRevokeOnLogout()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "session@example.com",
            "SecurePass123",
            "STUDENT"));

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "session@example.com",
            "SecurePass123"));
        var loginSession = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // 1. Rotate refresh token legitimately
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionRequest(
            loginSession.RefreshToken));
        Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rotatedSession = (await refreshResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        Assert.That(rotatedSession.RefreshToken, Is.Not.EqualTo(loginSession.RefreshToken));

        // 2. Replay the old refresh token -> 401 Unauthorized AND invalidates the rotated token family!
        var replayResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionRequest(
            loginSession.RefreshToken));
        Assert.That(replayResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // 3. The rotated token in the same family must now also be rejected!
        var compromisedFamilyResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionRequest(
            rotatedSession.RefreshToken));
        Assert.That(compromisedFamilyResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // 4. Login again and verify Logout revokes the new refresh token
        var secondLogin = await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "session@example.com",
            "SecurePass123"))).Content.ReadFromJsonAsync<AuthSessionResponse>();

        var logoutResponse = await _client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(
            secondLogin!.RefreshToken));
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var refreshAfterLogout = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionRequest(
            secondLogin.RefreshToken));
        Assert.That(refreshAfterLogout.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task AuthorizationMatrix_ShouldEnforceAll11ScenariosFor401And403AndAllowedAccess()
    {
        // Register and login Student
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "student@example.com",
            "SecurePass123",
            "STUDENT"));
        var studentSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "student@example.com",
            "SecurePass123"))).Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // Register and login Landlord
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "landlord@example.com",
            "SecurePass123",
            "LANDLORD"));
        var landlordSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "landlord@example.com",
            "SecurePass123"))).Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // Provision Admin server-side and login
        await ProvisionAdminInDatabaseAsync("admin@roomtorentradar.local", "AdminPass123");
        var adminSession = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "admin@roomtorentradar.local",
            "AdminPass123"))).Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        // Scenario 1: Missing token -> 401
        var noTokenResponse = await _client.GetAsync("/api/v1/test-rbac/student");
        Assert.That(noTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Scenario 2: Invalid JWT signature -> 401
        var wrongSigToken = CreateCustomJwt(
            secret: "wrong-signing-secret-that-is-32-bytes-long!!",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, studentSession.UserId.ToString()),
                new Claim(ClaimTypes.Role, "STUDENT"),
                new Claim(AuthClaimTypes.SecurityStamp, "stamp")
            ],
            expiresUtc: DateTime.UtcNow.AddMinutes(10));
        var wrongSigResponse = await SendWithBearerAsync("/api/v1/test-rbac/student", wrongSigToken);
        Assert.That(wrongSigResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Scenario 3: Expired JWT -> 401
        var expiredToken = CreateCustomJwt(
            secret: AuthWebApplicationFactory.TestJwtSecret,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, studentSession.UserId.ToString()),
                new Claim(ClaimTypes.Role, "STUDENT"),
                new Claim(AuthClaimTypes.SecurityStamp, "stamp")
            ],
            expiresUtc: DateTime.UtcNow.AddMinutes(-5));
        var expiredResponse = await SendWithBearerAsync("/api/v1/test-rbac/student", expiredToken);
        Assert.That(expiredResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Scenario 4: Missing user identifier claim -> 401
        var missingSubToken = CreateCustomJwt(
            secret: AuthWebApplicationFactory.TestJwtSecret,
            claims:
            [
                new Claim(ClaimTypes.Role, "STUDENT"),
                new Claim(AuthClaimTypes.SecurityStamp, "stamp")
            ],
            expiresUtc: DateTime.UtcNow.AddMinutes(10));
        var missingSubResponse = await SendWithBearerAsync("/api/v1/test-rbac/student", missingSubToken);
        Assert.That(missingSubResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Scenario 6: Student accessing Student policy -> 200 OK
        var studentToStudent = await SendWithBearerAsync("/api/v1/test-rbac/student", studentSession.AccessToken);
        Assert.That(studentToStudent.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Scenario 7: Student accessing Landlord policy -> 403 Forbidden
        var studentToLandlord = await SendWithBearerAsync("/api/v1/test-rbac/landlord", studentSession.AccessToken);
        Assert.That(studentToLandlord.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // Scenario 8: Landlord accessing Landlord policy -> 200 OK
        var landlordToLandlord = await SendWithBearerAsync("/api/v1/test-rbac/landlord", landlordSession.AccessToken);
        Assert.That(landlordToLandlord.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Scenario 9: Landlord accessing Student policy -> 403 Forbidden
        var landlordToStudent = await SendWithBearerAsync("/api/v1/test-rbac/student", landlordSession.AccessToken);
        Assert.That(landlordToStudent.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // Scenario 10: Non-Admin accessing Admin policy -> 403 Forbidden
        var studentToAdmin = await SendWithBearerAsync("/api/v1/test-rbac/admin", studentSession.AccessToken);
        var landlordToAdmin = await SendWithBearerAsync("/api/v1/test-rbac/admin", landlordSession.AccessToken);
        Assert.Multiple(() =>
        {
            Assert.That(studentToAdmin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(landlordToAdmin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });

        // Scenario 11: Admin accessing Admin policy -> 200 OK
        var adminToAdmin = await SendWithBearerAsync("/api/v1/test-rbac/admin", adminSession.AccessToken);
        Assert.That(adminToAdmin.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Scenario 5: User deactivated after token issued -> existing JWT must immediately return 401 Unauthorized!
        await DeactivateUserInDatabaseAsync(studentSession.UserId);
        var inactiveUserResponse = await SendWithBearerAsync("/api/v1/test-rbac/student", studentSession.AccessToken);
        Assert.That(inactiveUserResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task RateLimiting_WhenAuthRequestsExceedPermitLimit_ShouldReturn429TooManyRequests()
    {
        using var rateLimitedFactory = new AuthWebApplicationFactory(authPermitLimit: 3);
        using var client = rateLimitedFactory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var allowedResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
                "user@example.com",
                "WrongPass123"));
            Assert.That(allowedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        var throttledResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "user@example.com",
            "WrongPass123"));

        var problem = await throttledResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(throttledResponse.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(429));
        });
    }

    [Test]
    public async Task GoogleAuth_WhenConcurrentFirstTimeRegistrationsShareSameSubject_ShouldEnsureOnlyOneUserIsCreated()
    {
        _factory.GoogleVerifier.RegisterValidToken(
            "concurrent-token-1",
            "shared-concurrent-sub",
            "concurrent.user1@gmail.com");

        _factory.GoogleVerifier.RegisterValidToken(
            "concurrent-token-2",
            "shared-concurrent-sub",
            "concurrent.user2@gmail.com");

        var request1 = _client.PostAsJsonAsync("/api/v1/auth/google", new LoginWithGoogleRequest(
            "concurrent-token-1",
            "STUDENT"));

        var request2 = _client.PostAsJsonAsync("/api/v1/auth/google", new LoginWithGoogleRequest(
            "concurrent-token-2",
            "STUDENT"));

        var responses = await Task.WhenAll(request1, request2);

        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Multiple(() =>
        {
            Assert.That(okCount, Is.EqualTo(1), "Exactly one registration should succeed.");
            Assert.That(conflictCount, Is.EqualTo(1), "Concurrent registration with same Google subject should return 409 Conflict.");
        });
    }

    private async Task<HttpResponseMessage> SendWithBearerAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
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

    private static string CreateCustomJwt(
        string secret,
        IEnumerable<Claim> claims,
        DateTime expiresUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = AuthWebApplicationFactory.TestIssuer,
            Audience = AuthWebApplicationFactory.TestAudience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expiresUtc.AddMinutes(-10),
            IssuedAt = expiresUtc.AddMinutes(-10),
            Expires = expiresUtc,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
