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
            Assert.That(login.Role, Is.EqualTo("student"));
            Assert.That(login.ExpiresAt, Is.GreaterThan(DateTime.UtcNow));
        });
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
            new string('a', AuthLimits.PasswordMaxLength + 1));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_login_request");
    }

    [Test]
    public async Task Login_EmailTooLong_ReturnsBadRequestProblem()
    {
        var response = await LoginAsync(
            new string('a', AuthLimits.EmailMaxLength + 1),
            TestUsersSeed.Password);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_login_request");
    }

    [Test]
    public async Task Register_Student_ReturnsCreatedAndLoginReturnsStudentRole()
    {
        var response = await RegisterAsync("New.Student@Example.test", "Password1", "New Student", "student");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var registered = await response.Content.ReadFromJsonAsync<RegisterResponse>(ApiJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(registered, Is.Not.Null);
            Assert.That(registered!.UserId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(registered.Email, Is.EqualTo("new.student@example.test"));
            Assert.That(registered.DisplayName, Is.EqualTo("New Student"));
            Assert.That(registered.Role, Is.EqualTo("student"));
        });

        var login = await LoginAndReadAsync("NEW.STUDENT@EXAMPLE.TEST", "Password1");
        Assert.That(login.Role, Is.EqualTo("student"));
    }

    [Test]
    public async Task Register_Landlord_ReturnsLandlordRoleOnLogin()
    {
        var response = await RegisterAsync("landlord@example.test", "Password1", "Landlord One", "landlord");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var login = await LoginAndReadAsync("landlord@example.test", "Password1");
        Assert.That(login.Role, Is.EqualTo("landlord"));
    }

    [Test]
    public async Task Register_DuplicateEmail_IsCaseInsensitive()
    {
        var response = await RegisterAsync(
            TestUsersSeed.Email.ToUpperInvariant(),
            "Password1",
            "Duplicate User",
            "student");

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "email_already_registered");
    }

    [TestCase("Short1")]
    [TestCase("abcdefgh")]
    [TestCase("12345678")]
    public async Task Register_WeakPassword_ReturnsBadRequestProblem(string password)
    {
        var response = await RegisterAsync("weak@example.test", password, "Weak User", "student");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "weak_password");
    }

    [TestCase("admin")]
    [TestCase("moderator")]
    public async Task Register_NonSelfServiceRole_ReturnsBadRequestProblem(string role)
    {
        var response = await RegisterAsync("admin@example.test", "Password1", "Admin User", role);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_role");
    }

    [Test]
    public async Task Register_StoresHashedPassword()
    {
        const string password = "Password1";
        var response = await RegisterAsync("hashed@example.test", password, "Hashed User", "student");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Email == "hashed@example.test");

        Assert.Multiple(() =>
        {
            Assert.That(user.PasswordHash, Is.Not.EqualTo(password));
            Assert.That(user.PasswordSalt, Is.Not.Empty);
            Assert.That(new PasswordHasher().Verify(password, user.PasswordHash, user.PasswordSalt), Is.True);
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(storedCount, Is.EqualTo(1));
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

    private async Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password,
        string displayName,
        string role)
        => await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, password, displayName, role),
            ApiJson.Options);

    private async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            ApiJson.Options);
        return response;
    }

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
