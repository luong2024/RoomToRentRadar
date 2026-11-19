using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingHouseRulesEndpointTests
{
    private static readonly Guid ManagedListingId = TestListingsSeed.IdFor(2);

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
        await TestListingsSeed.SeedAsync(dbContext);
        await TestUsersSeed.SeedAsync(dbContext);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetDetail_ListingWithHouseRules_ReturnsStructuredRules()
    {
        var listingId = ManagedListingId;
        await SeedHouseRulesAsync(listingId, version: 3);

        var detail = await GetDetailAsync(listingId);

        Assert.That(detail.HouseRules, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(detail.HouseRules!.QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(detail.HouseRules.GuestPolicy, Is.EqualTo("Guests leave before 22:00"));
            Assert.That(detail.HouseRules.CookingRules, Is.EqualTo("Cook in the shared kitchen"));
            Assert.That(detail.HouseRules.ParkingRules, Is.EqualTo("Park on the ground floor"));
            Assert.That(detail.HouseRules.Version, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetDetail_ListingWithoutHouseRules_ReturnsNullSection()
    {
        var detail = await GetDetailAsync(TestListingsSeed.IdFor(1));

        Assert.That(detail.HouseRules, Is.Null);
    }

    [Test]
    public async Task GetManaged_ListingWithHouseRules_ReturnsStructuredRules()
    {
        var listingId = ManagedListingId;
        await AssignOwnerAsync(listingId);
        await SeedHouseRulesAsync(listingId, version: 3);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/listings/{listingId}/manage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync());
        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listing = await response.Content.ReadFromJsonAsync<ManagedListingDto>(ApiJson.Options);
        Assert.That(listing, Is.Not.Null);
        Assert.That(listing!.HouseRules, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(listing.HouseRules!.QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(listing.HouseRules.GuestPolicy, Is.EqualTo("Guests leave before 22:00"));
            Assert.That(listing.HouseRules.CookingRules, Is.EqualTo("Cook in the shared kitchen"));
            Assert.That(listing.HouseRules.ParkingRules, Is.EqualTo("Park on the ground floor"));
            Assert.That(listing.HouseRules.Version, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Unpublish_ByOwner_ReturnsHouseRules()
    {
        var listingId = ManagedListingId;
        await AssignOwnerAsync(listingId);
        await SeedHouseRulesAsync(listingId, version: 2);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/listings/{listingId}/unpublish");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync());
        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listing = await response.Content.ReadFromJsonAsync<ManagedListingDto>(ApiJson.Options);
        Assert.That(listing, Is.Not.Null);
        Assert.That(listing!.HouseRules, Is.Not.Null);
        Assert.That(listing.HouseRules!.Version, Is.EqualTo(2));
    }

    [Test]
    public async Task Update_ByOwner_CreatesFirstVersionAndDetailReflectsIt()
    {
        var listingId = ManagedListingId;
        var ownerId = await AssignOwnerAsync(listingId);

        var response = await PutHouseRulesAsync(listingId, CreateRequest(), await AccessTokenAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rules = await ReadHouseRulesAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(rules.Version, Is.EqualTo(1));
            Assert.That(rules.QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(rules.GuestPolicy, Is.EqualTo("Guests leave before 22:00"));
            Assert.That(rules.CookingRules, Is.EqualTo("Cook in the shared kitchen"));
            Assert.That(rules.ParkingRules, Is.EqualTo("Park on the ground floor"));
        });

        var detail = await GetDetailAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(detail.HouseRules, Is.Not.Null);
            Assert.That(detail.HouseRules!.Version, Is.EqualTo(1));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.Published));
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ListingHouseRules.AsNoTracking().SingleAsync(x => x.ListingId == listingId);
        Assert.Multiple(() =>
        {
            Assert.That(stored.QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(stored.Version, Is.EqualTo(1));
            Assert.That(stored.UpdatedByUserId, Is.EqualTo(ownerId));
        });
    }

    [Test]
    public async Task Update_Twice_IncrementsVersionAndKeepsHistory()
    {
        var listingId = ManagedListingId;
        await AssignOwnerAsync(listingId);
        var token = await AccessTokenAsync();

        var first = await PutHouseRulesAsync(listingId, CreateRequest(), token);
        Assert.That((await ReadHouseRulesAsync(first)).Version, Is.EqualTo(1));

        var updated = CreateRequest(
            quietHours: "23:00 - 05:00",
            guestPolicy: "No overnight guests",
            cookingRules: "Kitchen closes at 22:00",
            parkingRules: "Basement B1 only");
        var second = await PutHouseRulesAsync(listingId, updated, token);

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ReadHouseRulesAsync(second)).Version, Is.EqualTo(2));

        var detail = await GetDetailAsync(listingId);
        Assert.That(detail.HouseRules, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(detail.HouseRules!.Version, Is.EqualTo(2));
            Assert.That(detail.HouseRules.QuietHours, Is.EqualTo("23:00 - 05:00"));
            Assert.That(detail.HouseRules.GuestPolicy, Is.EqualTo("No overnight guests"));
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versions = await dbContext.ListingHouseRuleVersions
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderBy(x => x.Version)
            .ToListAsync();

        Assert.That(versions, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(versions[0].Version, Is.EqualTo(1));
            Assert.That(versions[0].QuietHours, Is.EqualTo("22:00 - 06:00"));
            Assert.That(versions[1].Version, Is.EqualTo(2));
            Assert.That(versions[1].QuietHours, Is.EqualTo("23:00 - 05:00"));
            Assert.That(versions[1].CookingRules, Is.EqualTo("Kitchen closes at 22:00"));
        });
    }

    [Test]
    public async Task Update_Anonymous_ReturnsUnauthorizedProblem()
    {
        var response = await PutHouseRulesAsync(
            ManagedListingId,
            CreateRequest(),
            token: null);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("unauthorized"));
    }

    [Test]
    public async Task Update_ByNonOwner_ReturnsListingNotFoundProblem()
    {
        var listingId = ManagedListingId;
        var otherToken = await SeedSecondUserAndLoginAsync();

        var response = await PutHouseRulesAsync(listingId, CreateRequest(), otherToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.ListingNotFound));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(
            await dbContext.ListingHouseRules.AnyAsync(x => x.ListingId == listingId),
            Is.False);
    }

    [Test]
    public async Task Update_WithInvalidFields_ReturnsFieldErrorsAndSavesNothing()
    {
        var listingId = ManagedListingId;
        await AssignOwnerAsync(listingId);

        var invalid = CreateRequest(
            quietHours: "   ",
            guestPolicy: new string('a', HouseRulesValidator.MaxFieldLength + 1));
        var response = await PutHouseRulesAsync(listingId, invalid, await AccessTokenAsync());

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        var errorFields = problem
            .GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(
                problem.GetProperty("code").GetString(),
                Is.EqualTo(ListingWriteProblemFactory.InvalidHouseRules));
            Assert.That(errorFields, Is.EquivalentTo(new[] { "quiet_hours", "guest_policy" }));
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasRules = await dbContext.ListingHouseRules.AnyAsync(x => x.ListingId == listingId);
        var hasVersions = await dbContext.ListingHouseRuleVersions
            .AnyAsync(x => x.ListingId == listingId);
        Assert.Multiple(() =>
        {
            Assert.That(hasRules, Is.False);
            Assert.That(hasVersions, Is.False);
        });
    }

    private static UpdateHouseRulesRequest CreateRequest(
        string quietHours = "22:00 - 06:00",
        string guestPolicy = "Guests leave before 22:00",
        string cookingRules = "Cook in the shared kitchen",
        string parkingRules = "Park on the ground floor") => new()
    {
        QuietHours = quietHours,
        GuestPolicy = guestPolicy,
        CookingRules = cookingRules,
        ParkingRules = parkingRules
    };

    private async Task SeedHouseRulesAsync(Guid listingId, long version)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.ListingHouseRules.Add(new ListingHouseRules
        {
            ListingId = listingId,
            QuietHours = "22:00 - 06:00",
            GuestPolicy = "Guests leave before 22:00",
            CookingRules = "Cook in the shared kitchen",
            ParkingRules = "Park on the ground floor",
            Version = version,
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<ListingDetailDto> GetDetailAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/v1/listings/{id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var detail = await response.Content.ReadFromJsonAsync<ListingDetailDto>(ApiJson.Options);
        Assert.That(detail, Is.Not.Null);
        return detail!;
    }

    private async Task<HttpResponseMessage> PutHouseRulesAsync(
        Guid listingId,
        UpdateHouseRulesRequest body,
        string? token)
    {
        using var content = JsonContent.Create(body, options: ApiJson.Options);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/listings/{listingId}/house-rules")
        {
            Content = content
        };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private static async Task<HouseRulesDto> ReadHouseRulesAsync(HttpResponseMessage response)
    {
        var rules = await response.Content.ReadFromJsonAsync<HouseRulesDto>(ApiJson.Options);
        Assert.That(rules, Is.Not.Null);
        return rules!;
    }

    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<Guid> AssignOwnerAsync(Guid listingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = await dbContext.Users
            .Where(x => x.Email == TestUsersSeed.Email)
            .Select(x => x.Id)
            .SingleAsync();
        var listing = await dbContext.Listings.SingleAsync(x => x.Id == listingId);
        listing.OwnerId = ownerId;
        await dbContext.SaveChangesAsync();
        return ownerId;
    }

    private async Task<string> AccessTokenAsync()
        => await LoginAsync(TestUsersSeed.Email, TestUsersSeed.Password);

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }

    private async Task<string> SeedSecondUserAndLoginAsync()
    {
        const string email = "other@example.test";
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (hash, salt) = PasswordHasher.Hash(TestUsersSeed.Password);
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Other Landlord",
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        return await LoginAsync(email, TestUsersSeed.Password);
    }
}
