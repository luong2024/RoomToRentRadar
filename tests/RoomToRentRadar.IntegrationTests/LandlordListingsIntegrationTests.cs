namespace RoomToRentRadar.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;
using VerificationStatus = RoomToRentRadar.Domain.Enums.VerificationStatus;

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
    public async Task CreateListing_FiveNineMebibytePhotos_ExceedsFormerLimitAndFitsConfiguredMultipartLimit()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        using var form = CreateForm(0);
        for (var i = 0; i < 5; i++)
        {
            var photo = new ByteArrayContent(new byte[9 * 1024 * 1024]);
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
    public async Task UpdateListing_OmittedRetainImageField_PreservesExistingGallery()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        using var form = CreateForm(0, "Updated without changing images");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/landlord/listings/{created.ListingId}") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        var response = await _client.SendAsync(request);
        var updated = await response.Content.ReadFromJsonAsync<ListingWriteResponse>();
        var saved = await _factory.RoomRepository.GetByIdAsync(created.ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updated!.ImageUrls, Is.EqualTo(created.ImageUrls));
            Assert.That(saved!.Title, Is.EqualTo("Updated without changing images"));
        });
    }

    [Test]
    public async Task UpdateListing_ExplicitReplaceGallery_SavesOnlyNewPhotos()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        using var form = CreateForm(3);
        form.Add(new StringContent("true"), "replaceGallery");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/landlord/listings/{created.ListingId}") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<ListingWriteResponse>();
        var saved = await _factory.RoomRepository.GetByIdAsync(created.ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(updated!.ImageUrls, Has.Count.EqualTo(3));
            Assert.That(updated.ImageUrls.Intersect(created.ImageUrls), Is.Empty);
            Assert.That(saved!.ImageUrls, Is.EqualTo(updated.ImageUrls));
            Assert.That(_factory.ListingImageStorage.DeletedUrls, Is.EquivalentTo(created.ImageUrls));
        });
    }

    [Test]
    public async Task UnpublishListing_OwnerRequest_PreservesImagesAndRemovesListingFromPublicSearch()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        await _factory.RoomListingRepository.AddAsync(new RoomListing(
            created.ListingId,
            "Integration listing",
            "Test description",
            new Money(2_500_000m, "VND"),
            22,
            "1 Test Street",
            new GeoLocation(21.5853, 105.8075),
            ListingRoomStatus.Available,
            VerificationStatus.Verified,
            "campus-ictu",
            created.ImageUrls.FirstOrDefault()));
        var room = await _factory.RoomRepository.GetByIdAsync(created.ListingId);
        room!.Status = RoomStatus.Available;
        room.IsVerified = true;
        _factory.RoomRepository.Upsert(room);
        var initialPublicResponse = await _client.GetAsync("/api/v1/rooms?CampusId=campus-ictu");
        var initialPublicListings = await initialPublicResponse.Content.ReadFromJsonAsync<PagedResult<RoomListingDto>>();
        Assert.That(initialPublicListings!.Items.Any(item => item.Id == created.ListingId), Is.True,
            "The initial public listing request should warm the versioned campus cache.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/landlord/listings/{created.ListingId}/unpublish");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        var response = await _client.SendAsync(request);
        var saved = await _factory.RoomRepository.GetByIdAsync(created.ListingId);
        // Ensure the same route the app uses is refreshed after unpublish.
        var publicResponse = await _client.GetAsync("/api/rooms");
        var publicListings = await publicResponse.Content.ReadFromJsonAsync<SearchRoomsResult>();
        var appPublicResponse = await _client.GetAsync("/api/v1/rooms?CampusId=campus-ictu");
        var appPublicListings = await appPublicResponse.Content.ReadFromJsonAsync<PagedResult<RoomListingDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(saved!.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(saved.IsVerified, Is.False);
            Assert.That(saved.ImageUrls, Is.EqualTo(created.ImageUrls));
            Assert.That(publicListings!.Items.Any(item => item.Id == created.ListingId), Is.False);
            Assert.That(appPublicListings!.Items.Any(item => item.Id == created.ListingId), Is.False);
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

    private static MultipartFormDataContent CreateForm(int photoCount, string title = "Integration listing")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(title), "title");
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
