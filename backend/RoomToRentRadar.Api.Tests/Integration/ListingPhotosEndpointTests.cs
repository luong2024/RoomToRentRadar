using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingPhotosEndpointTests
{
    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;
    private string _token = null!;

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
        var owner = await TestUsersSeed.SeedAsync(dbContext, UserRole.Landlord);
        await dbContext.Listings.ExecuteUpdateAsync(
            setters => setters.SetProperty(listing => listing.OwnerId, owner.Id));
        _token = await LoginAsync(TestUsersSeed.Email, TestUsersSeed.Password);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Upload_WithSingleJpeg_CreatesPhotoRowStoredFilesAndServesMedia()
    {
        var listingId = TestListingsSeed.IdFor(1);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateJpeg(640, 480), "image/jpeg", "holiday-photo.jpg"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.ToString(), Is.EqualTo($"/api/v1/listings/{listingId}/photos"));

        var photos = await ReadPhotosAsync(response);
        Assert.That(photos, Has.Count.EqualTo(1));

        var photo = photos[0];
        Assert.Multiple(() =>
        {
            Assert.That(photo.ListingId, Is.EqualTo(listingId));
            Assert.That(photo.Url, Is.EqualTo($"/media/listings/{listingId}/photo-{photo.Id:N}.jpg"));
            Assert.That(photo.ThumbUrl, Is.EqualTo($"/media/listings/{listingId}/thumbs/photo-{photo.Id:N}.jpg"));
            Assert.That(photo.Width, Is.EqualTo(640));
            Assert.That(photo.Height, Is.EqualTo(480));
            Assert.That(photo.Kind, Is.EqualTo(ListingPhotoKind.Photo));
            Assert.That(photo.SortOrder, Is.Zero);
            Assert.That(photo.IsCover, Is.True);
            Assert.That(photo.Alt, Is.Null);
        });

        var rows = await GetPhotoRowsAsync(listingId);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].Url, Is.EqualTo(photo.Url));
            Assert.That(rows[0].ThumbUrl, Is.EqualTo(photo.ThumbUrl));
            Assert.That(rows[0].IsCover, Is.True);
            Assert.That(rows[0].SortOrder, Is.Zero);
        });

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(OriginalFilePath(listingId, photo.Id)), Is.True);
            Assert.That(File.Exists(ThumbnailFilePath(listingId, photo.Id)), Is.True);
        });

        var originalResponse = await _client.GetAsync(photo.Url);
        var thumbnailResponse = await _client.GetAsync(photo.ThumbUrl);
        Assert.Multiple(() =>
        {
            Assert.That(originalResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(originalResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
            Assert.That(thumbnailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(thumbnailResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
        });
    }

    [Test]
    public async Task Upload_WithPanoramaKind_SetsPhotoKind()
    {
        var listingId = TestListingsSeed.IdFor(1);

        var response = await UploadWithKindAsync(
            listingId,
            "Panorama",
            (TestImageFactory.CreatePng(1200, 400), "image/png", "pano.png"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var photos = await ReadPhotosAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(1));
            Assert.That(photos[0].Kind, Is.EqualTo(ListingPhotoKind.Panorama));
            Assert.That(photos[0].Width, Is.EqualTo(1200));
            Assert.That(photos[0].Height, Is.EqualTo(400));
        });
    }

    [Test]
    public async Task Upload_WithMultipleFiles_ContinuesSortOrderAndKeepsExistingCover()
    {
        var listingId = TestListingsSeed.IdFor(3);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"),
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "b.jpg"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var photos = await ReadPhotosAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(photos.Select(x => x.SortOrder), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(photos.Select(x => x.IsCover), Is.EqualTo(new[] { false, false }));
        });

        var rows = await GetPhotoRowsAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(3));
            Assert.That(rows.Count(x => x.IsCover), Is.EqualTo(1));
            Assert.That(rows.Single(x => x.IsCover).SortOrder, Is.Zero);
        });
    }

    [Test]
    public async Task Upload_ToListingWithoutPhotos_MarksOnlyFirstPhotoAsCover()
    {
        var listingId = TestListingsSeed.IdFor(1);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"),
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "b.jpg"),
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "c.jpg"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var photos = await ReadPhotosAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(photos.Select(x => x.SortOrder), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(photos.Select(x => x.IsCover), Is.EqualTo(new[] { true, false, false }));
        });
    }

    [Test]
    public async Task GetGallery_ReturnsCoverFirstThenSortOrder()
    {
        var listingId = TestListingsSeed.IdFor(1);
        await SeedPhotoRowAsync(listingId, "first.jpg", sortOrder: 1, isCover: false);
        await SeedPhotoRowAsync(listingId, "cover.jpg", sortOrder: 5, isCover: true);
        await SeedPhotoRowAsync(listingId, "second.jpg", sortOrder: 3, isCover: false);

        var response = await _client.GetAsync($"/api/v1/listings/{listingId}/photos");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var photos = await ReadPhotosAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(3));
            Assert.That(photos.Select(x => x.SortOrder), Is.EqualTo(new[] { 5, 1, 3 }));
            Assert.That(photos.Select(x => x.IsCover), Is.EqualTo(new[] { true, false, false }));
        });
    }

    [Test]
    public async Task GetGallery_UnknownListing_ReturnsListingNotFoundProblem()
    {
        var response = await _client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}/photos");

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.ListingNotFound));
    }

    [Test]
    public async Task GetGallery_DraftListingAnonymous_ReturnsListingUnavailableProblem()
    {
        var listingId = TestListingsSeed.IdFor(20);

        var response = await GetGalleryAsync(listingId, token: null);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Gone);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(PhotoProblemFactory.ListingUnavailable));
    }

    [Test]
    public async Task GetGallery_HiddenListingAnonymous_ReturnsListingUnavailableProblem()
    {
        var listingId = TestListingsSeed.IdFor(22);

        var response = await GetGalleryAsync(listingId, token: null);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Gone);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(PhotoProblemFactory.ListingUnavailable));
    }

    [Test]
    public async Task GetGallery_DraftListingForOwner_ReturnsPhotos()
    {
        var listingId = TestListingsSeed.IdFor(20);

        var response = await GetGalleryAsync(listingId, _token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var photos = await ReadPhotosAsync(response);
        Assert.That(photos, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Upload_Anonymous_ReturnsUnauthorizedProblem()
    {
        using var multipart = CreateMultipart(
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"));

        var response = await PostPhotosAsync(TestListingsSeed.IdFor(1), multipart, token: null);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("unauthorized"));
    }

    [Test]
    public async Task Upload_AsStudent_ReturnsForbidden()
    {
        var listingId = TestListingsSeed.IdFor(1);
        const string email = "student@example.test";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TestUsersSeed.SeedAdditionalAsync(
                dbContext,
                Guid.NewGuid(),
                email,
                UserRole.Student,
                "Student Tester");
        }

        var studentToken = await LoginAsync(email, TestUsersSeed.Password);
        using var multipart = CreateMultipart(
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"));

        var response = await PostPhotosAsync(listingId, multipart, studentToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("forbidden"));
        });
        Assert.That(await GetPhotoRowsAsync(listingId), Is.Empty);
        Assert.That(StoredFileCount(listingId), Is.Zero);
    }

    [Test]
    public async Task Upload_ByNonOwner_ReturnsListingNotFoundProblem()
    {
        var listingId = TestListingsSeed.IdFor(1);
        var otherToken = await SeedSecondUserAndLoginAsync();
        using var multipart = CreateMultipart(
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"));

        var response = await PostPhotosAsync(listingId, multipart, otherToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        var rows = await GetPhotoRowsAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.ListingNotFound));
            Assert.That(rows, Is.Empty);
        });
    }

    [Test]
    public async Task Upload_UnknownListing_ReturnsListingNotFoundProblem()
    {
        var response = await UploadAsync(
            Guid.NewGuid(),
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.ListingNotFound));
    }

    [Test]
    public async Task Upload_WithoutFiles_ReturnsEmptyUploadProblem()
    {
        var listingId = TestListingsSeed.IdFor(1);
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent("photo"), "kind" }
        };

        var response = await PostPhotosAsync(listingId, multipart, _token);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.EmptyUpload));
    }

    [Test]
    public async Task Upload_WithCorruptJpeg_ReturnsInvalidPhotoProblemAndStoresNothing()
    {
        var listingId = TestListingsSeed.IdFor(1);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateCorruptJpeg(), "image/jpeg", "broken.jpg"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        var rows = await GetPhotoRowsAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.InvalidPhoto));
            Assert.That(rows, Is.Empty);
            Assert.That(StoredFileCount(listingId), Is.Zero);
        });
    }

    [Test]
    public async Task Upload_WithTextPlainFile_ReturnsUnsupportedPhotoTypeProblem()
    {
        var listingId = TestListingsSeed.IdFor(1);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateText(), "text/plain", "notes.txt"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
        Assert.That(
            problem.GetProperty("code").GetString(),
            Is.EqualTo(PhotoProblemFactory.UnsupportedPhotoType));
    }

    [Test]
    public async Task Upload_WhenListingAlreadyHasTwentyPhotos_ReturnsPhotoLimitExceededProblem()
    {
        var listingId = TestListingsSeed.IdFor(1);
        await SeedPhotosAsync(listingId, PhotoUploadLimits.MaxPhotosPerListing);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "a.jpg"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict);
        var rows = await GetPhotoRowsAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.PhotoLimitExceeded));
            Assert.That(rows, Has.Count.EqualTo(PhotoUploadLimits.MaxPhotosPerListing));
            Assert.That(StoredFileCount(listingId), Is.Zero);
        });
    }

    [Test]
    public async Task Upload_WithMixedValidAndCorruptFiles_PersistsNothing()
    {
        var listingId = TestListingsSeed.IdFor(1);

        var response = await UploadAsync(
            listingId,
            (TestImageFactory.CreateJpeg(400, 300), "image/jpeg", "good.jpg"),
            (TestImageFactory.CreateCorruptJpeg(), "image/jpeg", "broken.jpg"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        var rows = await GetPhotoRowsAsync(listingId);
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(PhotoProblemFactory.InvalidPhoto));
            Assert.That(rows, Is.Empty);
            Assert.That(StoredFileCount(listingId), Is.Zero);
        });
    }

    [TestCase("image/webp", "photo.webp", HttpStatusCode.UnsupportedMediaType)]
    [TestCase("image/jpeg", "photo.jpg", HttpStatusCode.BadRequest)]
    public async Task Upload_WebpIncludingDisguisedContent_IsRejected(
        string contentType, string fileName, HttpStatusCode expectedStatus)
    {
        var listingId = TestListingsSeed.IdFor(1);
        using var response = await UploadAsync(listingId,
            (TestImageFactory.CreateWebp(), contentType, fileName));
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That(await GetPhotoRowsAsync(listingId), Is.Empty);
        Assert.That(StoredFileCount(listingId), Is.Zero);
    }

    private async Task<HttpResponseMessage> UploadAsync(
        Guid listingId,
        params (byte[] Content, string ContentType, string FileName)[] files)
    {
        using var multipart = CreateMultipart(files);
        return await PostPhotosAsync(listingId, multipart, _token);
    }

    private async Task<HttpResponseMessage> UploadWithKindAsync(
        Guid listingId,
        string kind,
        params (byte[] Content, string ContentType, string FileName)[] files)
    {
        using var multipart = CreateMultipart(files);
        multipart.Add(new StringContent(kind), "kind");
        return await PostPhotosAsync(listingId, multipart, _token);
    }

    private async Task<HttpResponseMessage> PostPhotosAsync(
        Guid listingId,
        HttpContent content,
        string? token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/listings/{listingId}/photos")
        {
            Content = content
        };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetGalleryAsync(Guid listingId, string? token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/listings/{listingId}/photos");

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

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
        var (hash, salt) = new PasswordHasher().Hash(TestUsersSeed.Password);
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Other Landlord",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Landlord,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        return await LoginAsync(email, TestUsersSeed.Password);
    }

    private static MultipartFormDataContent CreateMultipart(
        params (byte[] Content, string ContentType, string FileName)[] files)
    {
        var multipart = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            multipart.Add(fileContent, "files", file.FileName);
        }

        return multipart;
    }

    private static async Task<List<ListingPhotoDto>> ReadPhotosAsync(HttpResponseMessage response)
    {
        var photos = await response.Content.ReadFromJsonAsync<List<ListingPhotoDto>>(ApiJson.Options);
        Assert.That(photos, Is.Not.Null);
        return photos!;
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

    private async Task SeedPhotosAsync(Guid listingId, int count)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var index = 0; index < count; index++)
        {
            dbContext.ListingPhotos.Add(CreatePhotoRow(listingId, $"seed-{index}.jpg", index, index == 0));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedPhotoRowAsync(Guid listingId, string fileName, int sortOrder, bool isCover)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.ListingPhotos.Add(CreatePhotoRow(listingId, fileName, sortOrder, isCover));
        await dbContext.SaveChangesAsync();
    }

    private static ListingPhoto CreatePhotoRow(Guid listingId, string fileName, int sortOrder, bool isCover) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = listingId,
        Url = $"/media/listings/{listingId}/{fileName.Replace(".jpg", string.Empty)}-{sortOrder}.jpg",
        ThumbUrl = $"/media/listings/{listingId}/thumbs/{fileName.Replace(".jpg", string.Empty)}-{sortOrder}.jpg",
        Width = 800,
        Height = 600,
        Kind = ListingPhotoKind.Photo,
        SortOrder = sortOrder,
        IsCover = isCover
    };

    private string ListingMediaDirectory(Guid listingId)
        => Path.Combine(_factory.MediaRootPath, "listings", listingId.ToString("D"));

    private string OriginalFilePath(Guid listingId, Guid photoId)
        => Path.Combine(ListingMediaDirectory(listingId), $"photo-{photoId:N}.jpg");

    private string ThumbnailFilePath(Guid listingId, Guid photoId)
        => Path.Combine(ListingMediaDirectory(listingId), "thumbs", $"photo-{photoId:N}.jpg");

    private int StoredFileCount(Guid listingId)
    {
        var directory = ListingMediaDirectory(listingId);
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length
            : 0;
    }
}
