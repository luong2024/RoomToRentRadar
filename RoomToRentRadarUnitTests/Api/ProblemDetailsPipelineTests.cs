// ADR-012, system-architecture 7.2, T00.5 error contract: through the real HTTP pipeline every
// 401/403/404/415/4xx/5xx is application/problem+json with a stable errorCode; 405 stays bare.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class ProblemDetailsPipelineTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory("Development");
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment, Action<IServiceCollection>? configureTestServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment(environment);
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");

            if (configureTestServices is not null)
            {
                webHost.ConfigureTestServices(configureTestServices);
            }
        });

    [Test]
    public async Task Get_MeWithoutToken_Returns401UnauthorizedProblem()
    {
        var response = await _client.GetAsync("/api/auth/me");

        await ReadProblemAsync(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.WwwAuthenticate, Is.Not.Empty);
            Assert.That(response.Headers.WwwAuthenticate.Any(header => header.Scheme == "Bearer"), Is.True);
        });
    }

    [Test]
    public async Task Get_MeWithGarbageToken_Returns401UnauthorizedProblem()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await _client.SendAsync(request);

        await ReadProblemAsync(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Test]
    public async Task Get_AdminEndpointAsStudent_Returns403ForbiddenProblem()
    {
        var token = await RegisterStudentAsync("admin-endpoint-student@ictu.edu.vn");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit-logs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        await ReadProblemAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [TestCase(null)]
    [TestCase("text/html")]
    public async Task Get_UnknownRoute_Returns404NotFoundProblem(string? accept)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/nope-this-route-does-not-exist");
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        var response = await _client.SendAsync(request);

        await ReadProblemAsync(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Test]
    public async Task Post_LoginWithTextPlain_Returns415UnsupportedMediaTypeProblem()
    {
        using var content = new StringContent("email=a", Encoding.UTF8, "text/plain");

        var response = await _client.PostAsync("/api/auth/login", content);

        await ReadProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    [Test]
    public async Task Get_MethodNotAllowed_StaysBare405()
    {
        // No ProblemCodes value exists for 405; status pages intentionally leave it bare (plan T00.5 Q9).
        var response = await _client.GetAsync("/api/auth/login");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(response.Content.Headers.ContentType, Is.Null);
        });
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
    }

    [Test]
    public async Task Post_LoginWrongPassword_Returns401InvalidCredentials()
    {
        const string email = "wrong-password@ictu.edu.vn";
        await RegisterStudentAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassw0rd" });

        await ReadProblemAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [Test]
    public async Task Post_RegisterDuplicateEmail_Returns409EmailTaken()
    {
        const string email = "duplicate-email@ictu.edu.vn";
        await RegisterStudentAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rdA1",
            fullName = "Student Pipeline",
            role = "Student"
        });

        await ReadProblemAsync(response, HttpStatusCode.Conflict, "EMAIL_TAKEN");
    }

    [Test]
    public async Task Post_RegisterAdminRole_Returns403ForbiddenRole()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "admin-role@ictu.edu.vn",
            password = "Passw0rdA1",
            fullName = "Student Pipeline",
            role = "Admin"
        });

        await ReadProblemAsync(response, HttpStatusCode.Forbidden, "FORBIDDEN_ROLE");
    }

    [Test]
    public async Task Get_RoomsInvalidSort_Returns400InvalidSortOption()
    {
        var response = await _client.GetAsync("/api/rooms?sort=foo");

        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_SORT_OPTION");
    }

    [Test]
    public async Task Get_RoomsModelBindingError_ErrorsKeysAreCamelCase()
    {
        var response = await _client.GetAsync("/api/rooms?minRentVnd=abc&roomTypes=Foo");

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        var errors = problem.GetProperty("errors");
        Assert.Multiple(() =>
        {
            Assert.That(errors.TryGetProperty("minRentVnd", out _), Is.True);
            Assert.That(errors.TryGetProperty("MinRentVnd", out _), Is.False);
            Assert.That(errors.TryGetProperty("RoomTypes", out _), Is.False);
            Assert.That(
                errors.EnumerateObject().Select(property => property.Name),
                Has.All.Matches<string>(name => name.Length == 0 || !char.IsUpper(name[0])));
        });
    }

    [Test]
    public async Task Post_LoginMalformedJson_ErrorsKeysAreCamelCase()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        var errors = problem.GetProperty("errors");
        Assert.That(
            errors.EnumerateObject().Select(property => property.Name),
            Has.All.Matches<string>(name => name.Length == 0 || !char.IsUpper(name[0])));
    }

    [Test]
    public Task UnhandledException_Development_Returns500InternalErrorWithoutStackTrace() =>
        AssertUnhandledExceptionIsHiddenAsync("Development");

    [Test]
    public Task UnhandledException_Production_Returns500InternalErrorWithoutStackTrace() =>
        AssertUnhandledExceptionIsHiddenAsync("Production");

    private async Task AssertUnhandledExceptionIsHiddenAsync(string environment)
    {
        using var factory = CreateFactory(environment, services => services.AddScoped<ISearchRoomsUseCase, ThrowingSearchRoomsUseCase>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/rooms");
        var problem = await ReadProblemAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR");

        Assert.That(problem.GetProperty("detail").GetString(), Is.EqualTo("An unexpected error occurred."));

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(rawBody, Does.Not.Contain("secret detail"));
            Assert.That(rawBody, Does.Not.Contain("   at "));
            Assert.That(rawBody, Does.Not.Contain("InvalidOperationException"));
            Assert.That(rawBody, Does.Not.Contain("exception").IgnoreCase);
            Assert.That(rawBody, Does.Not.Contain("ThrowingSearchRoomsUseCase"));
        });
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedErrorCode)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = body.RootElement.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo((int)expectedStatus));
            Assert.That(problem.GetProperty("errorCode").GetString(), Is.EqualTo(expectedErrorCode));
        });

        return problem;
    }

    private Task<string> RegisterStudentAsync(string email) => RegisterStudentAsync(_client, email);

    private static async Task<string> RegisterStudentAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Passw0rdA1",
            fullName = "Student Pipeline",
            role = "Student"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private sealed class ThrowingSearchRoomsUseCase : ISearchRoomsUseCase
    {
        public Task<PagedResult<RoomCardDto>> ExecuteAsync(RoomSearchRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("secret detail SELECT * FROM Rooms in C:/secret/path");
    }
}