using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.SqliteDataAccess;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class MediaEndpointTests
{
    private const string Origin = "http://example.test";

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
        _token = await LoginAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Media_AnonymousGet_ReturnsJpegWithImmutableCacheHeaders()
    {
        var photo = await UploadPhotoAsync();

        var original = await _client.GetAsync(photo.Url);
        var originalBody = await original.Content.ReadAsByteArrayAsync();
        var thumbnail = await _client.GetAsync(photo.ThumbUrl);
        var thumbnailBody = await thumbnail.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(original.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(original.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
            Assert.That(originalBody, Is.Not.Empty);
            Assert.That(original.Headers.GetValues("Cache-Control").Single(),
                Is.EqualTo("public, max-age=31536000, immutable"));

            Assert.That(thumbnail.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(thumbnail.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
            Assert.That(thumbnailBody, Is.Not.Empty);
            Assert.That(thumbnail.Headers.GetValues("Cache-Control").Single(),
                Is.EqualTo("public, max-age=31536000, immutable"));
        });
    }

    [Test]
    public async Task Media_GetWithOrigin_ReturnsWildcardCorsHeader()
    {
        var photo = await UploadPhotoAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, photo.Url);
        request.Headers.Add("Origin", Origin);
        var response = await _client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin").Single(), Is.EqualTo("*"));
        });
    }

    [Test]
    public async Task Media_PreflightForGet_AllowsGetMethod()
    {
        var photo = await UploadPhotoAsync();

        using var request = new HttpRequestMessage(HttpMethod.Options, photo.Url);
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin").Single(), Is.EqualTo("*"));
        Assert.That(
            response.Headers.GetValues("Access-Control-Allow-Methods").Single(),
            Does.Contain("GET"));
    }

    [Test]
    public async Task ListingsEndpoint_GetWithOrigin_DoesNotReturnCorsHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/listings");
        request.Headers.Add("Origin", Origin);
        var response = await _client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
        });
    }

    private async Task<ListingPhotoDto> UploadPhotoAsync()
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TestImageFactory.CreateJpeg(640, 480));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipart.Add(fileContent, "files", "gallery-photo.jpg");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/listings/{TestListingsSeed.IdFor(1)}/photos")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var photos = await response.Content.ReadFromJsonAsync<List<ListingPhotoDto>>(ApiJson.Options);
        Assert.That(photos, Is.Not.Null);
        return photos![0];
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(TestUsersSeed.Email, TestUsersSeed.Password),
            ApiJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiJson.Options);
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
