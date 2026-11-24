namespace RoomToRentRadar.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

public sealed class LandlordListingsIntegrationTests
{
    private AuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", "in-memory");
        Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", "IntegrationTestDb");
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
    public async Task CreateListing_LandlordRequest_PersistsPendingOwnerAndExcludesItFromPublicSearch()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        using var form = CreateForm(3);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<ListingWriteResponse>();
        var created = await _factory.RoomRepository.GetByIdAsync(result!.ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(result.Status, Is.EqualTo("Pending"));
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.LandlordId, Is.EqualTo(landlord.UserId.ToString("D")));
            Assert.That(created.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(created.IsVerified, Is.False);
            Assert.That(created.Deposit, Is.Zero);
            Assert.That(created.ImageUrls, Has.Count.EqualTo(3));
        });

        var publicResponse = await _client.GetAsync("/api/rooms");
        var publicListings = await publicResponse.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(publicListings!.Items.Any(item => item.Id == result.ListingId), Is.False);
    }

    [Test]
    public async Task CreateListing_StudentRole_IsForbidden()
    {
        var student = await RegisterAndLoginAsync("STUDENT");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = CreateForm(3) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", student.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var rooms = await _factory.RoomRepository.SearchAsync(new SearchRoomsQuery());
        Assert.That(rooms.TotalCount, Is.Zero);
    }

    [Test]
    public async Task CreateListing_UnauthenticatedRequest_IsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = CreateForm(3) };

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateListing_FewerThanThreePhotos_ReturnsValidationProblem()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = CreateForm(2) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var rooms = await _factory.RoomRepository.SearchAsync(new SearchRoomsQuery());
        Assert.That(rooms.TotalCount, Is.Zero);
    }

    [Test]
    public async Task CreateListing_ThreeTenMebibytePhotos_FitsConfiguredMultipartLimit()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        using var form = CreateForm(0);
        for (var i = 0; i < 3; i++)
        {
            var photo = new ByteArrayContent(new byte[10 * 1024 * 1024]);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(photo, "photos", $"large-{i}.png");
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task UpdateListing_AnotherLandlordCannotModifyListing()
    {
        var owner = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(owner.AccessToken);
        var other = await RegisterAndLoginAsync("LANDLORD");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/landlord/listings/{created.ListingId}") { Content = CreateForm(0) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await _factory.RoomRepository.GetByIdAsync(created.ListingId))!.Title, Is.EqualTo("Integration listing"));
    }

    [Test]
    public async Task UnpublishListing_OwnerRequest_PreservesImagesAndRemovesListingFromPublicSearch()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        var room = await _factory.RoomRepository.GetByIdAsync(created.ListingId);
        room!.Status = RoomStatus.Available;
        room.IsVerified = true;
        _factory.RoomRepository.Upsert(room);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/landlord/listings/{created.ListingId}/unpublish");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        var response = await _client.SendAsync(request);
        var saved = await _factory.RoomRepository.GetByIdAsync(created.ListingId);
        var publicResponse = await _client.GetAsync("/api/rooms");
        var publicListings = await publicResponse.Content.ReadFromJsonAsync<SearchRoomsResult>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(saved!.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(saved.IsVerified, Is.False);
            Assert.That(saved.ImageUrls, Is.EqualTo(created.ImageUrls));
            Assert.That(publicListings!.Items.Any(item => item.Id == created.ListingId), Is.False);
        });
    }

    private async Task<AuthSessionResponse> RegisterAndLoginAsync(string role)
    {
        var email = $"listing-{Guid.NewGuid():N}@example.test";
        var registration = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "StrongPassword123!", role));
        Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "StrongPassword123!"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    private async Task<ListingWriteResponse> CreateListingAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = CreateForm(3) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<ListingWriteResponse>())!;
    }

    private static MultipartFormDataContent CreateForm(int photoCount)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Integration listing"), "title");
        content.Add(new StringContent("1 Test Street"), "address");
        content.Add(new StringContent("2500000"), "monthlyRentVnd");
        content.Add(new StringContent("0"), "depositVnd");
        content.Add(new StringContent("22"), "areaM2");
        content.Add(new StringContent("BoardingRoom"), "roomType");
        content.Add(new StringContent("Test description"), "description");
        for (var i = 0; i < photoCount; i++)
        {
            var photo = new ByteArrayContent([1, 2, 3]);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(photo, "photos", $"photo-{i}.png");
        }
        return content;
    }
}
