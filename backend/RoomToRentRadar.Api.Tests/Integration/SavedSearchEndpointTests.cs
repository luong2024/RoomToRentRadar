using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class SavedSearchEndpointTests
{
    private static readonly Guid OtherUserId = new("bbbbbbbb-0000-0000-0000-000000000002");

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;

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
        var user = await TestUsersSeed.SeedAsync(dbContext);
        _userId = user.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Create_Anonymous_ReturnsUnauthorizedProblem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/saved-searches",
            ActiveRequest("Quận 10"),
            ApiJson.Options);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Test]
    public async Task Create_Active_ReturnsCreatedSearchAndStartsAlerting()
    {
        var response = await CreateAsync(await AccessTokenAsync(), ActiveRequest("  Quận 10  "));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<SavedSearchDto>(ApiJson.Options);
        Assert.That(created, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(created!.Name, Is.EqualTo("Quận 10"));
            Assert.That(created.AlertState, Is.EqualTo(SavedSearchAlertState.Active));
            Assert.That(created.Area.Type, Is.EqualTo("Polygon"));
            Assert.That(created.Area.Coordinates, Has.Length.EqualTo(1));
            Assert.That(created.Area.Coordinates![0], Has.Length.EqualTo(5));
            Assert.That(created.UpdatedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
            Assert.That(response.Headers.Location?.ToString(), Is.EqualTo($"/api/v1/saved-searches/{created.Id}"));
        });

        var stored = await FindSearchAsync(created!.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.UserId, Is.EqualTo(_userId));
            Assert.That(stored.AlertState, Is.EqualTo(SavedSearchAlertState.Active));
            Assert.That(stored.AlertsActivatedAt, Is.Not.Null);
            Assert.That(stored.AlertsActivatedAt!.Value, Is.EqualTo(created.UpdatedAt));
        });
    }

    [Test]
    public async Task Create_WithoutAlertState_DefaultsToOff()
    {
        var request = new CreateSavedSearchRequest
        {
            Name = "Bình Thạnh",
            Area = DefaultArea()
        };

        var response = await CreateAsync(await AccessTokenAsync(), request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<SavedSearchDto>(ApiJson.Options);
        Assert.That(created!.AlertState, Is.EqualTo(SavedSearchAlertState.Off));

        var stored = await FindSearchAsync(created.Id);
        Assert.That(stored.AlertsActivatedAt, Is.Null);
    }

    [Test]
    public async Task Create_NameTooLong_ReturnsInvalidNameProblem()
    {
        var request = new CreateSavedSearchRequest
        {
            Name = new string('a', 61),
            Area = DefaultArea()
        };

        var response = await CreateAsync(await AccessTokenAsync(), request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_saved_search_name");
    }

    [Test]
    public async Task Create_InvalidPolygon_ReturnsUnprocessableAreaProblem()
    {
        var request = new CreateSavedSearchRequest
        {
            Name = "Polygon lỗi",
            Area = new AreaPolygon
            {
                Type = "Polygon",
                Coordinates = [[[106.70, 10.70], [106.71, 10.71], [106.70, 10.70]]]
            }
        };

        var response = await CreateAsync(await AccessTokenAsync(), request);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "invalid_area_vertex_count");
    }

    [Test]
    public async Task SetAlert_PauseAndResume_MovesThroughEveryState()
    {
        var token = await AccessTokenAsync();
        var createResponse = await CreateAsync(
            token,
            new CreateSavedSearchRequest { Name = "Theo dõi", Area = DefaultArea() });
        var created = await createResponse.Content.ReadFromJsonAsync<SavedSearchDto>(ApiJson.Options);

        var activated = await SetAlertAsync(token, created!.Id, SavedSearchAlertState.Active);
        Assert.That(activated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var activatedDto = await activated.Content.ReadFromJsonAsync<SavedSearchDto>(ApiJson.Options);
        Assert.That(activatedDto!.AlertState, Is.EqualTo(SavedSearchAlertState.Active));

        var stored = await FindSearchAsync(created.Id);
        Assert.That(stored.AlertsActivatedAt, Is.Not.Null);
        var firstActivation = stored.AlertsActivatedAt;

        var paused = await SetAlertAsync(token, created.Id, SavedSearchAlertState.Paused);
        Assert.That(paused.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        stored = await FindSearchAsync(created.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.AlertState, Is.EqualTo(SavedSearchAlertState.Paused));
            Assert.That(stored.AlertsActivatedAt, Is.EqualTo(firstActivation));
        });

        await Task.Delay(30);
        var resumed = await SetAlertAsync(token, created.Id, SavedSearchAlertState.Active);
        Assert.That(resumed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        stored = await FindSearchAsync(created.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.AlertState, Is.EqualTo(SavedSearchAlertState.Active));
            Assert.That(stored.AlertsActivatedAt, Is.GreaterThan(firstActivation));
        });

        var off = await SetAlertAsync(token, created.Id, SavedSearchAlertState.Off);
        Assert.That(off.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var offDto = await off.Content.ReadFromJsonAsync<SavedSearchDto>(ApiJson.Options);
        Assert.That(offDto!.AlertState, Is.EqualTo(SavedSearchAlertState.Off));
    }

    [Test]
    public async Task SetAlert_UnknownId_ReturnsNotFoundProblem()
    {
        var response = await SetAlertAsync(
            await AccessTokenAsync(),
            Guid.NewGuid(),
            SavedSearchAlertState.Paused);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "saved_search_not_found");
    }

    [Test]
    public async Task SetAlert_AnotherUsersSearch_ReturnsNotFoundAndLeavesItUntouched()
    {
        var searchId = await SeedForeignSearchAsync();
        var response = await SetAlertAsync(
            await AccessTokenAsync(),
            searchId,
            SavedSearchAlertState.Paused);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "saved_search_not_found");
        Assert.That((await FindSearchAsync(searchId)).AlertState, Is.EqualTo(SavedSearchAlertState.Active));
    }

    [Test]
    public async Task Delete_RemovesSearchAndItsAlertLogs()
    {
        var token = await AccessTokenAsync();
        var createResponse = await CreateAsync(
            token,
            new CreateSavedSearchRequest { Name = "Xóa", Area = DefaultArea() });
        var created = await createResponse.Content.ReadFromJsonAsync<SavedSearchDto>(ApiJson.Options);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.SavedSearchAlertLogs.Add(new SavedSearchAlertLog
            {
                Id = Guid.NewGuid(),
                SavedSearchId = created!.Id,
                ListingId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                Delivered = true
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/saved-searches/{created!.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var searchRemains = await verifyDbContext.SavedSearches.AnyAsync(search => search.Id == created.Id);
        var logsRemain = await verifyDbContext.SavedSearchAlertLogs.AnyAsync(log => log.SavedSearchId == created.Id);

        Assert.Multiple(() =>
        {
            Assert.That(searchRemains, Is.False);
            Assert.That(logsRemain, Is.False);
        });
    }

    [Test]
    public async Task Delete_AnotherUsersSearch_ReturnsNotFoundAndKeepsIt()
    {
        var searchId = await SeedForeignSearchAsync();
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/saved-searches/{searchId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync());

        var response = await _client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "saved_search_not_found");
        Assert.That(await FindSearchAsync(searchId), Is.Not.Null);
    }

    private static CreateSavedSearchRequest ActiveRequest(string name) => new()
    {
        Name = name,
        AlertState = SavedSearchAlertState.Active,
        Area = DefaultArea()
    };

    private static AreaPolygon DefaultArea() => new()
    {
        Type = "Polygon",
        Coordinates =
        [
            [
                [106.69, 10.69],
                [106.71, 10.69],
                [106.71, 10.71],
                [106.69, 10.71],
                [106.69, 10.69]
            ]
        ]
    };

    private async Task<HttpResponseMessage> CreateAsync(string accessToken, CreateSavedSearchRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/saved-searches")
        {
            Content = JsonContent.Create(request, options: ApiJson.Options)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SetAlertAsync(
        string accessToken,
        Guid savedSearchId,
        SavedSearchAlertState alertState)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/saved-searches/{savedSearchId}/alert")
        {
            Content = JsonContent.Create(
                new SetSavedSearchAlertRequest { AlertState = alertState },
                options: ApiJson.Options)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(message);
    }

    private async Task<string> AccessTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(TestUsersSeed.Email, TestUsersSeed.Password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        return login!.AccessToken;
    }

    private async Task<SavedSearch> FindSearchAsync(Guid savedSearchId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.SavedSearches
            .AsNoTracking()
            .SingleAsync(search => search.Id == savedSearchId);
    }

    private async Task<Guid> SeedForeignSearchAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (hash, salt) = new PasswordHasher().Hash("Other-password-123");
        dbContext.Users.Add(new User
        {
            Id = OtherUserId,
            Email = "other@example.test",
            DisplayName = "Other Tester",
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = DateTime.UtcNow
        });

        var search = new SavedSearch
        {
            Id = Guid.NewGuid(),
            UserId = OtherUserId,
            Name = "Của người khác",
            AreaJson = JsonSerializer.Serialize(DefaultArea(), ApiJson.Options),
            AlertState = SavedSearchAlertState.Active,
            AlertsActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.SavedSearches.Add(search);
        await dbContext.SaveChangesAsync();
        return search.Id;
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
