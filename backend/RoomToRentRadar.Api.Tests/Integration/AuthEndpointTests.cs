using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class AuthEndpointTests
{
    private const string InvalidCode = "000000";

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
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Login_ValidCredentials_ReturnsAccessToken()
    {
        var response = await LoginAsync(TestUsersSeed.Email, TestUsersSeed.Password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(login, Is.Not.Null);
            Assert.That(login!.AccessToken, Is.Not.Empty);
            Assert.That(login.DisplayName, Is.EqualTo(TestUsersSeed.DisplayName));
            Assert.That(login.Role, Is.EqualTo(UserRoleWire.Student));
            Assert.That(login.ExpiresAt, Is.GreaterThan(DateTime.UtcNow));
        });
    }

    [Test]
    public async Task Login_LandlordAccount_ReturnsLandlordRole()
    {
        const string email = "landlord@example.test";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TestUsersSeed.SeedAdditionalAsync(
                dbContext,
                Guid.NewGuid(),
                email,
                UserRole.Landlord,
                "Landlord Tester");
        }

        var response = await LoginAsync(email, TestUsersSeed.Password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login!.Role, Is.EqualTo(UserRoleWire.Landlord));
    }

    [Test]
    public async Task Login_ReturnsSignedJwtWithUserClaims()
    {
        var login = await LoginAndReadAsync();

        var segments = login.AccessToken.Split('.');
        Assert.That(segments, Has.Length.EqualTo(3));

        using var header = DecodeJwtSegment(segments[0]);
        using var payload = DecodeJwtSegment(segments[1]);

        Assert.Multiple(() =>
        {
            Assert.That(header.RootElement.GetProperty("alg").GetString(), Is.EqualTo("HS256"));
            Assert.That(payload.RootElement.GetProperty("iss").GetString(), Is.EqualTo("RoomToRentRadar.Api"));
            Assert.That(payload.RootElement.GetProperty("aud").GetString(), Is.EqualTo("RoomToRentRadar"));
            Assert.That(
                payload.RootElement.GetProperty("sub").GetString(),
                Is.EqualTo(TestUsersSeed.Id.ToString()));
            Assert.That(payload.RootElement.GetProperty("role").GetString(), Is.EqualTo("student"));
            Assert.That(
                payload.RootElement.GetProperty("email").GetString(),
                Is.EqualTo(TestUsersSeed.Email));
        });
    }

    [Test]
    public async Task ProtectedEndpoint_WithValidJwt_ReturnsOk()
    {
        var login = await LoginAndReadAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/favourites");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ProtectedEndpoint_WithTamperedJwt_ReturnsUnauthorizedProblem()
    {
        var login = await LoginAndReadAsync();
        var segments = login.AccessToken.Split('.');
        var signature = segments[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        segments[2] = new string(signature);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/favourites");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.Join('.', segments));
        var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Login_IsCaseInsensitiveForEmail()
    {
        var response = await LoginAsync("DEMO@EXAMPLE.TEST", TestUsersSeed.Password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Login_WrongPassword_ReturnsUnauthorizedProblem()
    {
        var response = await LoginAsync(TestUsersSeed.Email, "wrong-password");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Test]
    public async Task Login_UnknownEmail_ReturnsUnauthorizedProblem()
    {
        var response = await LoginAsync("missing@example.test", TestUsersSeed.Password);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Test]
    public async Task Login_MissingPassword_ReturnsBadRequestProblem()
    {
        var response = await LoginAsync(TestUsersSeed.Email, string.Empty);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_login_request");
    }

    [Test]
    public async Task Login_PasswordTooLong_ReturnsBadRequestProblem()
    {
        var response = await LoginAsync(
            TestUsersSeed.Email,
            new string('a', AccountRules.PasswordMaxLength + 1));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_login_request");
    }

    [Test]
    public async Task Login_EmailTooLong_ReturnsBadRequestProblem()
    {
        var response = await LoginAsync(
            new string('a', AccountRules.EmailMaxLength + 1),
            TestUsersSeed.Password);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_login_request");
    }

    [Test]
    public async Task Register_Student_SendsOtpAndVerificationCreatesTheAccount()
    {
        var challenge = await BeginRegistrationAsync(
            "New.Student@Example.test",
            "Password1",
            "New Student",
            "student");

        Assert.Multiple(() =>
        {
            Assert.That(challenge.ChallengeId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(challenge.Email, Is.EqualTo("new.student@example.test"));
            Assert.That(challenge.ExpiresAt, Is.GreaterThan(DateTime.UtcNow));
            Assert.That(_factory.EmailSink.Sent, Has.Count.EqualTo(1));
        });

        var email = _factory.EmailSink.Sent[0];
        Assert.Multiple(() =>
        {
            Assert.That(email.ToEmail, Is.EqualTo("new.student@example.test"));
            Assert.That(email.DisplayName, Is.EqualTo("New Student"));
            Assert.That(email.Code, Has.Length.EqualTo(AccountRules.OtpLength));
            Assert.That(email.Code.All(char.IsAsciiDigit), Is.True);
        });

        var login = await VerifyAndReadAsync(challenge.ChallengeId);
        Assert.Multiple(() =>
        {
            Assert.That(login.AccessToken, Is.Not.Empty);
            Assert.That(login.Role, Is.EqualTo(UserRoleWire.Student));
            Assert.That(login.DisplayName, Is.EqualTo("New Student"));
        });

        var laterLogin = await LoginAndReadAsync("NEW.STUDENT@EXAMPLE.TEST", "Password1");
        Assert.That(laterLogin.Role, Is.EqualTo(UserRoleWire.Student));
    }

    [Test]
    public async Task Register_Landlord_VerificationReturnsLandlordRole()
    {
        var challenge = await BeginRegistrationAsync(
            "landlord.otp@example.test",
            "Password1",
            "Landlord One",
            "landlord");

        var login = await VerifyAndReadAsync(challenge.ChallengeId);

        Assert.That(login.Role, Is.EqualTo(UserRoleWire.Landlord));
    }

    [Test]
    public async Task Register_DuplicateEmail_IsCaseInsensitiveAndSendsNoEmail()
    {
        var response = await PostRegisterAsync(
            TestUsersSeed.Email.ToUpperInvariant(),
            "Password1",
            "Duplicate User",
            "student");

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "email_already_registered");
        Assert.That(_factory.EmailSink.Sent, Is.Empty);
    }

    [TestCase("Short1")]
    [TestCase("abcdefgh")]
    [TestCase("12345678")]
    public async Task Register_WeakPassword_ReturnsBadRequestProblem(string password)
    {
        var response = await PostRegisterAsync("weak@example.test", password, "Weak User", "student");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "weak_password");
    }

    [TestCase("admin")]
    [TestCase("moderator")]
    public async Task Register_NonSelfServiceRole_ReturnsBadRequestProblem(string role)
    {
        var response = await PostRegisterAsync("admin@example.test", "Password1", "Admin User", role);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_role");
    }

    [Test]
    public async Task Register_DoesNotCreateUserBeforeVerification()
    {
        await BeginRegistrationAsync("pending@example.test", "Password1", "Pending User", "student");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Multiple(() =>
        {
            Assert.That(
                dbContext.Users.Any(user => user.Email == "pending@example.test"),
                Is.False);
            Assert.That(dbContext.RegistrationChallenges.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Register_StoresHashedSecretsWithATenMinuteLifetime()
    {
        var challenge = await BeginRegistrationAsync(
            "hashed.challenge@example.test",
            "Password1",
            "Hashed User",
            "student");
        var code = _factory.EmailSink.Sent[0].Code;

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.RegistrationChallenges
            .SingleAsync(candidate => candidate.Id == challenge.ChallengeId);
        var hasher = new PasswordHasher();

        Assert.Multiple(() =>
        {
            Assert.That(stored.PasswordHash, Is.Not.EqualTo("Password1"));
            Assert.That(hasher.Verify("Password1", stored.PasswordHash, stored.PasswordSalt), Is.True);
            Assert.That(stored.OtpHash, Is.Not.EqualTo(code));
            Assert.That(hasher.Verify(code, stored.OtpHash, stored.OtpSalt), Is.True);
            Assert.That(stored.AttemptCount, Is.Zero);
            Assert.That(
                stored.ExpiresAt - stored.CreatedAt,
                Is.EqualTo(TimeSpan.FromMinutes(AccountRules.OtpLifetimeMinutes)));
        });
    }

    [Test]
    public async Task Register_AgainForTheSameEmail_ReplacesThePreviousChallenge()
    {
        var first = await BeginRegistrationAsync(
            "resend@example.test",
            "Password1",
            "Resend User",
            "student");
        var second = await BeginRegistrationAsync(
            "RESEND@example.test",
            "Password1",
            "Resend User",
            "student");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await dbContext.RegistrationChallenges
                .Where(challenge => challenge.Email == "resend@example.test")
                .ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored, Has.Count.EqualTo(1));
                Assert.That(stored[0].Id, Is.EqualTo(second.ChallengeId));
            });
        }

        var replay = await VerifyAsync(first.ChallengeId, _factory.EmailSink.Sent[0].Code);
        await AssertProblemAsync(replay, HttpStatusCode.BadRequest, "invalid_otp_challenge");

        var login = await VerifyAndReadAsync(second.ChallengeId);
        Assert.That(login.Role, Is.EqualTo(UserRoleWire.Student));
    }

    [Test]
    public async Task Register_WhenEmailDeliveryFails_Returns502AndDropsTheChallenge()
    {
        _factory.EmailSink.FailSends = true;

        var response = await PostRegisterAsync(
            "delivery.failed@example.test",
            "Password1",
            "Delivery Failed",
            "student");

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "email_delivery_failed");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(
            dbContext.RegistrationChallenges.Any(
                challenge => challenge.Email == "delivery.failed@example.test"),
            Is.False);
    }

    [Test]
    public async Task Verify_WithCorrectCode_CreatesHashedUserSessionAndDeletesChallenge()
    {
        var challenge = await BeginRegistrationAsync(
            "verified@example.test",
            "Password1",
            "Verified User",
            "student");

        var login = await VerifyAndReadAsync(challenge.ChallengeId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Email == "verified@example.test");
        var sessionCount = await dbContext.AuthSessions.CountAsync(session => session.UserId == user.Id);
        var challengeCount = await dbContext.RegistrationChallenges.CountAsync();
        var hasher = new PasswordHasher();

        Assert.Multiple(() =>
        {
            Assert.That(user.PasswordHash, Is.Not.EqualTo("Password1"));
            Assert.That(hasher.Verify("Password1", user.PasswordHash, user.PasswordSalt), Is.True);
            Assert.That(user.Role, Is.EqualTo(UserRole.Student));
            Assert.That(sessionCount, Is.EqualTo(1));
            Assert.That(challengeCount, Is.Zero);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/favourites");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var protectedResponse = await _client.SendAsync(request);
        Assert.That(protectedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Verify_InvalidCode_ReturnsAttemptsRemainingAndKeepsTheChallengeUsable()
    {
        var challenge = await BeginRegistrationAsync(
            "attempts@example.test",
            "Password1",
            "Attempts User",
            "student");

        var response = await VerifyAsync(challenge.ChallengeId, InvalidCode);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_otp");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(
            document.RootElement.GetProperty("attempts_remaining").GetInt32(),
            Is.EqualTo(AccountRules.OtpMaxAttempts - 1));

        var login = await VerifyAndReadAsync(challenge.ChallengeId);
        Assert.That(login.Role, Is.EqualTo(UserRoleWire.Student));
    }

    [Test]
    public async Task Verify_FifthWrongCode_Returns429AndDropsTheChallenge()
    {
        var challenge = await BeginRegistrationAsync(
            "exhausted@example.test",
            "Password1",
            "Exhausted User",
            "student");

        for (var attempt = 1; attempt < AccountRules.OtpMaxAttempts; attempt++)
        {
            var rejected = await VerifyAsync(challenge.ChallengeId, InvalidCode);
            await AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "invalid_otp");
        }

        var final = await VerifyAsync(challenge.ChallengeId, InvalidCode);
        await AssertProblemAsync(final, HttpStatusCode.TooManyRequests, "otp_attempts_exceeded");

        var correctCode = _factory.EmailSink.Sent[0].Code;
        var afterLockout = await VerifyAsync(challenge.ChallengeId, correctCode);
        await AssertProblemAsync(afterLockout, HttpStatusCode.BadRequest, "invalid_otp_challenge");
    }

    [Test]
    public async Task Verify_CodeIsSingleUse()
    {
        var challenge = await BeginRegistrationAsync(
            "single.use@example.test",
            "Password1",
            "Single Use",
            "student");
        await VerifyAndReadAsync(challenge.ChallengeId);

        var replay = await VerifyAsync(challenge.ChallengeId, _factory.EmailSink.Sent[0].Code);

        await AssertProblemAsync(replay, HttpStatusCode.BadRequest, "invalid_otp_challenge");
    }

    [Test]
    public async Task Verify_ExpiredChallenge_ReturnsBadRequestAndDropsTheChallenge()
    {
        var challenge = await BeginRegistrationAsync(
            "expired@example.test",
            "Password1",
            "Expired User",
            "student");
        var code = _factory.EmailSink.Sent[0].Code;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await dbContext.RegistrationChallenges
                .SingleAsync(candidate => candidate.Id == challenge.ChallengeId);
            stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await VerifyAsync(challenge.ChallengeId, code);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_otp_challenge");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(
            document.RootElement.GetProperty("title").GetString(),
            Is.EqualTo("Verification code expired"));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That(
                dbContext.RegistrationChallenges.Any(
                    candidate => candidate.Id == challenge.ChallengeId),
                Is.False);
        }
    }

    [Test]
    public async Task Verify_UnknownChallenge_ReturnsBadRequestProblem()
    {
        var response = await VerifyAsync(Guid.NewGuid(), _factory.OtpCodes.NextCode);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_otp_challenge");
    }

    [Test]
    public async Task Verify_EmptyChallengeId_ReturnsBadRequestProblem()
    {
        var response = await VerifyAsync(Guid.Empty, _factory.OtpCodes.NextCode);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_otp_request");
    }

    [TestCase("12345")]
    [TestCase("1234567")]
    [TestCase("abcdef")]
    [TestCase("")]
    public async Task Verify_MalformedCode_ReturnsBadRequestProblem(string code)
    {
        var response = await VerifyAsync(Guid.NewGuid(), code);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_otp_request");
    }

    [Test]
    public async Task Verify_WhenEmailWasRegisteredMeanwhile_ReturnsConflictWithoutSession()
    {
        var challenge = await BeginRegistrationAsync(
            "race.verify@example.test",
            "Password1",
            "Race Verify",
            "student");
        var code = _factory.EmailSink.Sent[0].Code;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var (hash, salt) = new PasswordHasher().Hash("Password1");
            dbContext.Users.Add(CreateUser("race.verify@example.test", hash, salt));
            await dbContext.SaveChangesAsync();
        }

        var response = await VerifyAsync(challenge.ChallengeId, code);
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "email_already_registered");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Multiple(() =>
            {
                Assert.That(
                    dbContext.AuthSessions.Count(),
                    Is.Zero);
                Assert.That(
                    dbContext.Users.Count(user => user.Email == "race.verify@example.test"),
                    Is.EqualTo(1));
                Assert.That(
                    dbContext.RegistrationChallenges.Any(
                        candidate => candidate.Id == challenge.ChallengeId),
                    Is.False);
            });
        }
    }

    [Test]
    public async Task Logout_ValidToken_InvalidatesTheSession()
    {
        var login = await LoginAndReadAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var logout = await _client.SendAsync(request);
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var secondLogout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        secondLogout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await _client.SendAsync(secondLogout);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Logout_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var response = await _client.PostAsync("/api/v1/auth/logout", content: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task DemoLogin_OutsideDevelopment_ReturnsNotFound()
    {
        var response = await _client.PostAsync("/api/v1/auth/demo-login", content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Register_WithNullBody_ReturnsBadRequestProblem()
    {
        using var content = new StringContent("null", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/register", content);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }

    [Test]
    public async Task Verify_WithNullBody_ReturnsBadRequestProblem()
    {
        using var content = new StringContent("null", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/register/verify", content);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }

    [Test]
    public async Task TryAddAsync_WhenEmailAlreadyInserted_ReturnsFalse()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (hash, salt) = new PasswordHasher().Hash("Password1");

        var first = await users.TryAddAsync(
            CreateUser("race@example.test", hash, salt),
            CancellationToken.None);
        var second = await users.TryAddAsync(
            CreateUser("race@example.test", hash, salt),
            CancellationToken.None);
        var storedCount = await dbContext.Users.CountAsync(user => user.Email == "race@example.test");
        var addedEntries = dbContext.ChangeTracker
            .Entries<User>()
            .Count(entry => entry.State == EntityState.Added);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(storedCount, Is.EqualTo(1));
            Assert.That(addedEntries, Is.Zero);
        });
    }

    [Test]
    public async Task AuthenticatedRequest_WithExpiredSession_ReturnsUnauthorizedProblem()
    {
        var login = await LoginAndReadAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sessions = await dbContext.AuthSessions.ToListAsync();
            Assert.That(sessions, Is.Not.Empty);

            foreach (var session in sessions)
            {
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            }

            await dbContext.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    private static User CreateUser(string email, string hash, string salt)
        => new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Race User",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };

    private static JsonDocument DecodeJwtSegment(string segment)
    {
        var base64 = segment.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return JsonDocument.Parse(Convert.FromBase64String(base64));
    }

    private async Task<RegisterChallengeResponse> BeginRegistrationAsync(
        string email,
        string password,
        string displayName,
        string role)
    {
        var response = await PostRegisterAsync(email, password, displayName, role);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

        var challenge = await response.Content.ReadFromJsonAsync<RegisterChallengeResponse>(ApiJson.Options);
        Assert.That(challenge, Is.Not.Null);
        return challenge!;
    }

    private Task<HttpResponseMessage> PostRegisterAsync(
        string email,
        string password,
        string displayName,
        string role)
        => _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, password, displayName, role),
            ApiJson.Options);

    private Task<HttpResponseMessage> VerifyAsync(Guid challengeId, string code)
        => _client.PostAsJsonAsync(
            "/api/v1/auth/register/verify",
            new VerifyRegistrationRequest(challengeId, code),
            ApiJson.Options);

    private async Task<LoginResponse> VerifyAndReadAsync(Guid challengeId)
    {
        var response = await VerifyAsync(challengeId, _factory.EmailSink.Sent[^1].Code);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login, Is.Not.Null);
        return login!;
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password)
        => await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            ApiJson.Options);

    private async Task<LoginResponse> LoginAndReadAsync()
        => await LoginAndReadAsync(TestUsersSeed.Email, TestUsersSeed.Password);

    private async Task<LoginResponse> LoginAndReadAsync(string email, string password)
    {
        var response = await LoginAsync(email, password);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login, Is.Not.Null);
        return login!;
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
