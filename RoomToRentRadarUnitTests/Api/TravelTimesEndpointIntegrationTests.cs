using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// US09 / T09.1 — <c>operationId: getRoomTravelTimes</c> endpoint integration & fail-fast tests.
/// </summary>
[TestFixture]
public class TravelTimesEndpointIntegrationTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public void GetRoomTravelTimes_Action_HasHttpGetAndAllowAnonymous()
    {
        var action = typeof(RoomsController).GetMethod(nameof(RoomsController.GetRoomTravelTimes));

        Assert.That(action, Is.Not.Null);
        Assert.Multiple(() =>
        {
            var httpGet = action!.GetCustomAttribute<HttpGetAttribute>();
            Assert.That(httpGet, Is.Not.Null);
            Assert.That(httpGet!.Template, Is.EqualTo("{roomId}/travel-times"));
            Assert.That(action.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Not.Null);
        });
    }

    [Test]
    public async Task GetRoomTravelTimes_AnonymousWithMockRouting_ReturnsAllThreeModesAndCamelCaseEnumStrings()
    {
        var response = await _client.GetAsync("/api/rooms/seed-z115/travel-times");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("campusId").GetString(), Is.EqualTo("ICTU"));
            Assert.That(root.GetProperty("isFallback").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("straightLineDistanceKm").GetDouble(), Is.GreaterThan(0));

            var items = root.GetProperty("items").EnumerateArray().ToList();
            Assert.That(items, Has.Count.EqualTo(3));
            Assert.That(items[0].GetProperty("mode").GetString(), Is.EqualTo("Motorbike"));
            Assert.That(items[0].GetProperty("isEstimate").GetBoolean(), Is.False);
            Assert.That(items[1].GetProperty("mode").GetString(), Is.EqualTo("Bus"));
            Assert.That(items[1].GetProperty("isEstimate").GetBoolean(), Is.True);
            Assert.That(items[2].GetProperty("mode").GetString(), Is.EqualTo("Walking"));
            Assert.That(items[2].GetProperty("isEstimate").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task GetRoomTravelTimes_WithCampusQuery_UsesRequestedCampus()
    {
        var response = await _client.GetAsync("/api/rooms/seed-z115/travel-times?campusId=TNUE");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<TravelTimesDto>(JsonOptions);

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.CampusId, Is.EqualTo("TNUE"));
            Assert.That(dto.IsFallback, Is.False);
            Assert.That(dto.Items, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task GetRoomTravelTimes_AuthenticatedStudentWithProfileCampus_UsesSelectedCampus()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var student = User.Reconstitute(
                id: "student-travel-1",
                email: "student-travel@ictu.edu.vn",
                fullName: "Travel Student",
                passwordHash: "hash",
                role: UserRole.Student,
                authProvider: AuthProvider.Local,
                ssoSubjectId: null,
                phoneNumber: null,
                studentCode: "DTC999",
                isVerified: true,
                failedLoginCount: 0,
                lockoutEndUtc: null,
                createdAtUtc: new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc),
                updatedAtUtc: new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc),
                refreshTokens: null,
                selectedCampusId: "TNUS",
                preferredLanguage: "vi",
                version: 1);
            await userRepo.AddAsync(student);

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", GenerateJwt(student.Id, "Student"));
        }

        var response = await _client.GetAsync("/api/rooms/seed-z115/travel-times");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<TravelTimesDto>(JsonOptions);
        Assert.That(dto!.CampusId, Is.EqualTo("TNUS"));
    }

    [Test]
    public async Task GetRoomTravelTimes_WhenRoutingReturnsNull_ReturnsStraightLineFallbackAndDoesNotCache()
    {
        using var fallbackFactory = CreateFactory(configureTestServices: services =>
        {
            services.AddSingleton<ITravelTimePort>(new AlwaysNullTravelTimePort());
        });
        using var client = fallbackFactory.CreateClient();

        var response = await client.GetAsync("/api/rooms/seed-z115/travel-times");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<TravelTimesDto>(JsonOptions);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.IsFallback, Is.True);
            Assert.That(dto.Items, Is.Empty);
            Assert.That(dto.StraightLineDistanceKm, Is.GreaterThan(0));
        });

        using var scope = fallbackFactory.Services.CreateScope();
        var cacheRepo = scope.ServiceProvider.GetRequiredService<ITravelTimeCacheRepository>();
        var cached = await cacheRepo.GetByRoomAndCampusAsync("seed-z115", "ICTU");
        Assert.That(cached, Is.Empty);
    }

    [Test]
    public async Task GetRoomTravelTimes_UnknownCampus_Returns400CampusNotFound()
    {
        var response = await _client.GetAsync("/api/rooms/seed-z115/travel-times?campusId=NOPE");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    public async Task GetRoomTravelTimes_UnknownRoom_Returns404RoomNotFound()
    {
        var response = await _client.GetAsync("/api/rooms/does-not-exist/travel-times");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task GetRoomTravelTimes_WhitespaceRoomId_Returns400ValidationFailed()
    {
        var response = await _client.GetAsync("/api/rooms/%20%20/travel-times");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.ValidationFailed));
    }

    [Test]
    public void NonDevelopment_WithMockRoutingEnabled_ThrowsOnStartup()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Production");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Routing:UseMock", "true");
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Test]
    public void NonDevelopment_WithoutOrsApiKey_ThrowsOnStartup()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Production");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Routing:UseMock", "false");
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configureTestServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "true");
            webHost.UseSetting("Sqlite:SeedExtendedDemoData", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
            webHost.UseSetting("Routing:UseMock", "true");

            if (configureTestServices is not null)
            {
                webHost.ConfigureTestServices(configureTestServices);
            }
        });

    private static string GenerateJwt(string userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "RoomToRentRadar.Api",
            audience: "RoomToRentRadar.Client",
            claims: new[] { new Claim("sub", userId), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class AlwaysNullTravelTimePort : ITravelTimePort
    {
        public Task<RoutingModesResult?> GetRoutesAsync(
            double originLat, double originLng, double destinationLat, double destinationLng, CancellationToken ct = default)
            => Task.FromResult<RoutingModesResult?>(null);
    }
}
