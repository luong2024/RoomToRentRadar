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
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingManagementEndpointTests
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
    public async Task Create_Anonymous_ReturnsUnauthorizedProblem()
    {
        using var form = CreateForm(DefaultFields(), DefaultPhotos());

        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", form, token: null);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("unauthorized"));
    }

    [Test]
    public async Task Create_WithThreeJpegPhotos_SavesPendingReviewListingOwnedByCaller()
    {
        var ownerId = await DemoUserIdAsync();
        using var form = CreateForm(DefaultFields(), DefaultPhotos());

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/listings",
            form,
            await AccessTokenAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var detail = await ReadDetailAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Location?.ToString(), Is.EqualTo($"/api/v1/listings/{detail.Id}"));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(detail.LandlordName, Is.EqualTo(TestUsersSeed.DisplayName));
            Assert.That(detail.Deposit, Is.EqualTo(4_000_000));
        });

        var rows = await GetPhotoRowsAsync(detail.Id);
        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(3));
            Assert.That(rows.Count(x => x.IsCover), Is.EqualTo(1));
            Assert.That(rows.Single(x => x.IsCover).SortOrder, Is.Zero);
            Assert.That(StoredFileCount(detail.Id), Is.EqualTo(6));
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.Id == detail.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.OwnerId, Is.EqualTo(ownerId));
            Assert.That(stored.Status, Is.EqualTo(ListingStatus.PendingReview));
        });

        var publicDetail = await _client.GetAsync($"/api/v1/listings/{detail.Id}");
        Assert.That(publicDetail.StatusCode, Is.EqualTo(HttpStatusCode.Gone));

        var publishedIds = await SearchIdsAsync();
        Assert.That(publishedIds, Does.Not.Contain(detail.Id));
    }

    [Test]
    public async Task Create_WithPngPhotos_IsAccepted()
    {
        var photos = new[]
        {
            (TestImageFactory.CreatePng(), "image/png", "a.png"),
            (TestImageFactory.CreatePng(), "image/png", "b.png"),
            (TestImageFactory.CreatePng(), "image/png", "c.png")
        };

        var detail = await CreateListingAsync(await AccessTokenAsync(), photos: photos);

        Assert.That(detail.Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public async Task Create_WithTwoPhotos_ReturnsTooFewPhotosAndCreatesNothing()
    {
        using var form = CreateForm(DefaultFields(), DefaultPhotos()[..2]);

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/listings",
            form,
            await AccessTokenAsync());

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        var ownerListingCount = await OwnerListingCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(
                problem.GetProperty("code").GetString(),
                Is.EqualTo(ListingWriteProblemFactory.TooFewPhotos));
            Assert.That(ownerListingCount, Is.Zero);
        });
    }

    [Test]
    public async Task Create_WithWebpPhoto_ReturnsUnsupportedPhotoType()
    {
        var photos = DefaultPhotos();
        photos[1] = (TestImageFactory.CreateWebp(), "image/webp", "b.webp");
        using var form = CreateForm(DefaultFields(), photos);

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/listings",
            form,
            await AccessTokenAsync());

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.UnsupportedPhotoType));
    }

    [Test]
    public async Task Create_WithPhotoOverTenMegabytes_ReturnsPhotoTooLarge()
    {
        var oversized = new byte[PhotoUploadLimits.MaxFileBytes + 1];
        var photos = DefaultPhotos();
        photos[0] = (oversized, "image/jpeg", "huge.jpg");
        using var form = CreateForm(DefaultFields(), photos);

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/listings",
            form,
            await AccessTokenAsync());

        var problem = await ReadProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.PhotoTooLarge));
    }

    [Test]
    public async Task Create_WithMissingRequiredFields_ReturnsFieldErrors()
    {
        var fields = DefaultFields()
            .Where(field => field.Name is not ("deposit" or "area_m2" or "room_type"))
            .ToArray();
        using var form = CreateForm(fields, DefaultPhotos());

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/listings",
            form,
            await AccessTokenAsync());

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
                Is.EqualTo(ListingWriteProblemFactory.InvalidListing));
            Assert.That(errorFields, Is.EquivalentTo(new[] { "deposit", "area_m2", "room_type" }));
        });
    }

    [Test]
    public async Task Create_WithCorruptPhoto_ReturnsInvalidPhotoAndCreatesNothing()
    {
        var photos = DefaultPhotos();
        photos[1] = (TestImageFactory.CreateCorruptJpeg(), "image/jpeg", "broken.jpg");
        using var form = CreateForm(DefaultFields(), photos);

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/listings",
            form,
            await AccessTokenAsync());

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        var ownerListingCount = await OwnerListingCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(
                problem.GetProperty("code").GetString(),
                Is.EqualTo(ListingWriteProblemFactory.InvalidPhoto));
            Assert.That(ownerListingCount, Is.Zero);
        });
    }

    [Test]
    public async Task Update_ByOwner_ChangesDetailsAndKeepsPendingStatus()
    {
        var token = await AccessTokenAsync();
        var created = await CreateListingAsync(token);

        using var form = CreateForm(
            DefaultFields(title: "Renovated room", price: "6000000"),
            photos: []);
        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/listings/{created.Id}",
            form,
            token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var detail = await ReadDetailAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(detail.Title, Is.EqualTo("Renovated room"));
            Assert.That(detail.Price, Is.EqualTo(6_000_000));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.PendingReview));
        });

        var rows = await GetPhotoRowsAsync(created.Id);
        Assert.That(rows, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Update_ByNonOwner_ReturnsNotFound()
    {
        var created = await CreateListingAsync(await AccessTokenAsync());
        var otherToken = await SeedSecondUserAndLoginAsync();

        using var form = CreateForm(DefaultFields(title: "Hijacked room"), photos: []);
        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/listings/{created.Id}",
            form,
            otherToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.ListingNotFound));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.That(stored.Title, Is.EqualTo(created.Title));
    }

    [Test]
    public async Task Update_WithAdditionalPhoto_AppendsPhotoAndKeepsCover()
    {
        var token = await AccessTokenAsync();
        var created = await CreateListingAsync(token);
        var coverId = (await GetPhotoRowsAsync(created.Id)).Single(x => x.IsCover).Id;

        using var form = CreateForm(
            DefaultFields(),
            [(TestImageFactory.CreateJpeg(), "image/jpeg", "extra.jpg")]);
        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/listings/{created.Id}",
            form,
            token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rows = await GetPhotoRowsAsync(created.Id);
        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(4));
            Assert.That(rows.Single(x => x.IsCover).Id, Is.EqualTo(coverId));
            Assert.That(rows.Select(x => x.SortOrder), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        });
    }

    [Test]
    public async Task Update_PublishedListing_KeepsPublishedStatus()
    {
        var ownerId = await DemoUserIdAsync();
        await AssignOwnerAsync(TestListingsSeed.IdFor(3), ownerId);
        var token = await AccessTokenAsync();

        using var form = CreateForm(DefaultFields(title: "Refreshed listing"), photos: []);
        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/listings/{TestListingsSeed.IdFor(3)}",
            form,
            token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var detail = await ReadDetailAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(detail.Title, Is.EqualTo("Refreshed listing"));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.Published));
        });
    }

    [Test]
    public async Task Update_UnknownListing_ReturnsNotFound()
    {
        using var form = CreateForm(DefaultFields(), photos: []);

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/listings/{Guid.NewGuid()}",
            form,
            await AccessTokenAsync());

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.ListingNotFound));
    }

    [Test]
    public async Task Unpublish_ByOwner_HidesListingAndIsIdempotent()
    {
        var token = await AccessTokenAsync();
        var created = await CreateListingAsync(token);

        var first = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/listings/{created.Id}/unpublish",
            content: null,
            token);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var detail = await ReadDetailAsync(first);
        Assert.That(detail.Status, Is.EqualTo(ListingStatus.Hidden));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.Id == created.Id);
            Assert.That(stored.Status, Is.EqualTo(ListingStatus.Hidden));
        }

        var second = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/listings/{created.Id}/unpublish",
            content: null,
            token);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ReadDetailAsync(second)).Status, Is.EqualTo(ListingStatus.Hidden));

        var publicDetail = await _client.GetAsync($"/api/v1/listings/{created.Id}");
        Assert.That(publicDetail.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        Assert.That(await SearchIdsAsync(), Does.Not.Contain(created.Id));
    }

    [Test]
    public async Task Unpublish_ByNonOwner_ReturnsNotFound()
    {
        var created = await CreateListingAsync(await AccessTokenAsync());
        var otherToken = await SeedSecondUserAndLoginAsync();

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/listings/{created.Id}/unpublish",
            content: null,
            otherToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.ListingNotFound));
    }

    [Test]
    public async Task GetManaged_ByOwner_ReturnsPendingListing()
    {
        var token = await AccessTokenAsync();
        var created = await CreateListingAsync(token);

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/listings/{created.Id}/manage",
            content: null,
            token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var detail = await ReadDetailAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(detail.Id, Is.EqualTo(created.Id));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.PendingReview));
        });
    }

    [Test]
    public async Task GetManaged_ByNonOwner_ReturnsNotFound()
    {
        var created = await CreateListingAsync(await AccessTokenAsync());
        var otherToken = await SeedSecondUserAndLoginAsync();

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/listings/{created.Id}/manage",
            content: null,
            otherToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(ListingWriteProblemFactory.ListingNotFound));
    }

    private static (string Name, string Value)[] DefaultFields(
        string title = "Sunny room near campus",
        string price = "5000000") =>
    [
        ("title", title),
        ("address", "12 Nguyen Trai, District 1"),
        ("price", price),
        ("deposit", "4000000"),
        ("area_m2", "25"),
        ("room_type", "studio"),
        ("bedrooms", "1"),
        ("description", "Bright room with a balcony."),
        ("lat", "10.76"),
        ("lng", "106.70")
    ];

    private static (byte[] Content, string ContentType, string FileName)[] DefaultPhotos() =>
    [
        (TestImageFactory.CreateJpeg(), "image/jpeg", "a.jpg"),
        (TestImageFactory.CreateJpeg(), "image/jpeg", "b.jpg"),
        (TestImageFactory.CreateJpeg(), "image/jpeg", "c.jpg")
    ];

    private static MultipartFormDataContent CreateForm(
        IEnumerable<(string Name, string Value)> fields,
        IEnumerable<(byte[] Content, string ContentType, string FileName)> photos)
    {
        var multipart = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
        {
            multipart.Add(new StringContent(value), name);
        }

        foreach (var photo in photos)
        {
            var fileContent = new ByteArrayContent(photo.Content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(photo.ContentType);
            multipart.Add(fileContent, "photos", photo.FileName);
        }

        return multipart;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        HttpContent? content,
        string? token)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private async Task<ListingDetailDto> CreateListingAsync(
        string token,
        (string Name, string Value)[]? fields = null,
        (byte[] Content, string ContentType, string FileName)[]? photos = null)
    {
        using var form = CreateForm(fields ?? DefaultFields(), photos ?? DefaultPhotos());
        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", form, token);
        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
        return await ReadDetailAsync(response);
    }

    private static async Task<ListingDetailDto> ReadDetailAsync(HttpResponseMessage response)
    {
        var detail = await response.Content.ReadFromJsonAsync<ListingDetailDto>(ApiJson.Options);
        Assert.That(detail, Is.Not.Null);
        return detail!;
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

    private async Task<Guid> DemoUserIdAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Users
            .Where(x => x.Email == TestUsersSeed.Email)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task AssignOwnerAsync(Guid listingId, Guid ownerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = await dbContext.Listings.SingleAsync(x => x.Id == listingId);
        listing.OwnerId = ownerId;
        await dbContext.SaveChangesAsync();
    }

    private async Task<int> OwnerListingCountAsync()
    {
        var ownerId = await DemoUserIdAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Listings.CountAsync(x => x.OwnerId == ownerId);
    }

    private async Task<List<ListingPhoto>> GetPhotoRowsAsync(Guid listingId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
    }

    private async Task<HashSet<Guid>> SearchIdsAsync()
    {
        var response = await _client.GetAsync("/api/v1/listings?limit=100");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToHashSet();
    }

    private int StoredFileCount(Guid listingId)
    {
        var directory = Path.Combine(_factory.MediaRootPath, "listings", listingId.ToString("D"));
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length
            : 0;
    }
}
