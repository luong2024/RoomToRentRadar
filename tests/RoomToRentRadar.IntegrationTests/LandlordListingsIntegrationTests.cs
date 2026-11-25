namespace RoomToRentRadar.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        if (TestContext.CurrentContext.Test.MethodName == nameof(CreateListing_TwentyTenMebibytePhotos_FitsKestrelMultipartLimits))
        {
            _factory.UseKestrel(0);
            // Avoid copying the full upload into RAM for automatic redirects.
            _factory.ClientOptions.AllowAutoRedirect = false;
        }
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
            Assert.That(result.Latitude, Is.Null);
            Assert.That(result.Longitude, Is.Null);
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.LandlordId, Is.EqualTo(landlord.UserId));
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
    [SetCulture("vi-VN")]
    public async Task ListingCoordinates_RoundTripInvariantMultipartThenKeepPairWhenOmittedOrBlank()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);
        using var create = CreateForm(3);
        create.Add(new StringContent(" 21.5944123456789 "), "latitude");
        create.Add(new StringContent("1.058482123456789e2"), "longitude");
        create.Add(new StringContent(Guid.NewGuid().ToString("D")), "ownerId");
        create.Add(new StringContent("Available"), "status");

        using var createdResponse = await _client.PostAsync("/api/v1/landlord/listings", create);
        Assert.That(createdResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = (await createdResponse.Content.ReadFromJsonAsync<ListingWriteResponse>())!;
        var path = $"/api/v1/landlord/listings/{created.ListingId}";

        using var replace = CreateForm(0);
        replace.Add(new StringContent("0"), "latitude");
        replace.Add(new StringContent("-180"), "longitude");
        using var replacedResponse = await _client.PutAsync(path, replace);
        Assert.That(replacedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var replaced = (await replacedResponse.Content.ReadFromJsonAsync<ListingWriteResponse>())!;

        using var omitted = CreateForm(0);
        using var omittedResponse = await _client.PutAsync(path, omitted);
        Assert.That(omittedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var omittedResult = (await omittedResponse.Content.ReadFromJsonAsync<ListingWriteResponse>())!;

        using var blank = CreateForm(0);
        blank.Add(new StringContent("  "), "latitude");
        blank.Add(new StringContent(""), "longitude");
        using var blankResponse = await _client.PutAsync(path, blank);
        Assert.That(blankResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var blankResult = (await blankResponse.Content.ReadFromJsonAsync<ListingWriteResponse>())!;
        var ownerRead = (await _client.GetFromJsonAsync<ListingCoordinatesResponse>(path))!;
        var publicRead = (await _client.GetFromJsonAsync<RoomDetailsDto>($"/api/rooms/{created.ListingId}"))!;
        var room = (await _factory.RoomRepository.GetByIdAsync(created.ListingId))!;

        Assert.Multiple(() =>
        {
            Assert.That(created.Latitude, Is.EqualTo(21.5944123456789));
            Assert.That(created.Longitude, Is.EqualTo(105.8482123456789));
            Assert.That(replaced.Latitude, Is.Zero);
            Assert.That(replaced.Longitude, Is.EqualTo(-180));
            Assert.That(omittedResult.Latitude, Is.EqualTo(replaced.Latitude));
            Assert.That(omittedResult.Longitude, Is.EqualTo(replaced.Longitude));
            Assert.That(blankResult.Latitude, Is.EqualTo(replaced.Latitude));
            Assert.That(blankResult.Longitude, Is.EqualTo(replaced.Longitude));
            Assert.That(ownerRead.ListingId, Is.EqualTo(created.ListingId));
            Assert.That(ownerRead.Latitude, Is.Zero);
            Assert.That(ownerRead.Longitude, Is.EqualTo(-180));
            Assert.That(publicRead.Latitude, Is.Null);
            Assert.That(publicRead.Longitude, Is.Null);
            Assert.That(room.LandlordId, Is.EqualTo(landlord.UserId));
            Assert.That(room.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(room.ImageUrls, Is.EqualTo(created.ImageUrls));
        });
    }

    [TestCase("21,5", "105.8", false, "latitude", TestName = "InvalidMultipartCoordinates_RejectDecimalComma")]
    [TestCase("2,150", "105.8", false, "latitude", TestName = "InvalidMultipartCoordinates_RejectThousandsSeparator")]
    [TestCase("null", "105.8", false, "latitude", TestName = "InvalidMultipartCoordinates_RejectLiteralNull")]
    [TestCase("21.5\0", "105.8", false, "latitude", TestName = "InvalidMultipartCoordinates_RejectNulSuffix")]
    [TestCase("21.5", "105.8", true, "latitude", TestName = "InvalidMultipartCoordinates_RejectDuplicateIgnoringCase")]
    [TestCase("21.5", null, false, "coordinates", TestName = "InvalidMultipartCoordinates_RejectOmittedLongitude")]
    [TestCase("21.5", " ", false, "coordinates", TestName = "InvalidMultipartCoordinates_RejectBlankLongitude")]
    [TestCase("NaN", "105.8", false, "coordinates", TestName = "InvalidMultipartCoordinates_RejectNonFinite")]
    [TestCase("21.5", "1e400", false, "coordinates", TestName = "InvalidMultipartCoordinates_RejectOverflow")]
    public async Task InvalidMultipartCoordinates_Return400AndKeepExistingListing(
        string latitude, string? longitude, bool duplicateLatitude, string expectedField)
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        var before = (await _factory.RoomRepository.GetByIdAsync(created.ListingId))!;
        var originalLocation = new GeoLocation(21.25, 105.75);
        before.SetLocation(originalLocation);
        var originalImages = before.ImageUrls.ToArray();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);
        using var form = CreateForm(1, "Do not save this title");
        form.Add(new StringContent(latitude), "latitude");
        if (longitude is not null) form.Add(new StringContent(longitude), "longitude");
        if (duplicateLatitude) form.Add(new StringContent(latitude), "LATITUDE");

        using var response = await _client.PutAsync($"/api/v1/landlord/listings/{created.ListingId}", form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var saved = (await _factory.RoomRepository.GetByIdAsync(created.ListingId))!;

        Assert.Multiple(() =>
        {
            Assert.That(problem.RootElement.GetProperty("errors").TryGetProperty(expectedField, out _), Is.True);
            Assert.That(saved.Title, Is.EqualTo("Integration listing"));
            Assert.That(saved.GetLocation(), Is.EqualTo(originalLocation));
            Assert.That(saved.ImageUrls, Is.EqualTo(originalImages));
            Assert.That(_factory.ListingImageStorage.StoredUrls, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task CreateListing_BlankCoordinateFieldsStoreNoLocation()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);
        using var form = CreateForm(3);
        form.Add(new StringContent(""), "latitude");
        form.Add(new StringContent("  "), "longitude");

        using var response = await _client.PostAsync("/api/v1/landlord/listings", form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var listing = (await response.Content.ReadFromJsonAsync<ListingWriteResponse>())!;
        var room = (await _factory.RoomRepository.GetByIdAsync(listing.ListingId))!;

        Assert.Multiple(() =>
        {
            Assert.That(listing.Latitude, Is.Null);
            Assert.That(listing.Longitude, Is.Null);
            Assert.That(room.GetLocation(), Is.Null);
        });
    }

    [Test]
    public async Task CreateListing_MultipartWithoutBoundary_Returns400WithoutUploading()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);
        using var malformed = new StringContent("invalid multipart body");
        malformed.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");

        using var response = await _client.PostAsync("/api/v1/landlord/listings", malformed);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(_factory.ListingImageStorage.StoredUrls, Is.Empty);
        });
    }

    [Test]
    public async Task GetListingCoordinates_RequiresLandlordAndConcealsListingsOwnedByOthers()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var anotherLandlord = await RegisterAndLoginAsync("LANDLORD");
        var student = await RegisterAndLoginAsync("STUDENT");
        var listing = await CreateListingAsync(landlord.AccessToken);
        var path = $"/api/v1/landlord/listings/{listing.ListingId}";

        using var anonymous = await _client.GetAsync(path);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", student.AccessToken);
        using var forbidden = await _client.GetAsync(path);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", anotherLandlord.AccessToken);
        using var wrongOwner = await _client.GetAsync(path);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);
        using var missing = await _client.GetAsync("/api/v1/landlord/listings/does-not-exist");
        using var own = await _client.GetAsync(path);

        Assert.That(own.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var existing = (await own.Content.ReadFromJsonAsync<ListingCoordinatesResponse>())!;
        Assert.Multiple(() =>
        {
            Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(wrongOwner.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(existing.Latitude, Is.Null);
            Assert.That(existing.Longitude, Is.Null);
        });
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
    public async Task CreateListing_TwentyTenMebibytePhotos_FitsKestrelMultipartLimits()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        using var form = CreateForm(0);
        var fileBytes = new byte[10 * 1024 * 1024];
        for (var i = 0; i < 20; i++)
        {
            var photo = new ByteArrayContent(fileBytes);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(photo, "photos", $"large-{i}.png");
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/landlord/listings") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);

        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var result = await response.Content.ReadFromJsonAsync<ListingWriteResponse>();
        Assert.That(result!.ImageUrls, Has.Count.EqualTo(20));
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
    public async Task UpdateListing_PublishedListing_IsPendingAndAbsentFromCachedAndFreshSearch()
    {
        const string publicSearch = "/api/v1/rooms?CampusId=campus-ictu";
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        await PublishListingForTestAsync(created);

        var before = await _client.GetFromJsonAsync<PagedResult<RoomListingDto>>(publicSearch);
        Assert.That(before!.Items.Select(item => item.Id), Does.Contain(created.ListingId));

        using var form = CreateForm(0, "Updated published listing");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/landlord/listings/{created.ListingId}") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlord.AccessToken);
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var saved = await _factory.RoomRepository.GetByIdAsync(created.ListingId);
        var warmedQuery = await _client.GetFromJsonAsync<PagedResult<RoomListingDto>>(publicSearch);
        // A different sort uses a new cache key and reads the projection again.
        var freshQuery = await _client.GetFromJsonAsync<PagedResult<RoomListingDto>>(publicSearch + "&sortBy=price");

        Assert.Multiple(() =>
        {
            Assert.That(saved!.Title, Is.EqualTo("Updated published listing"));
            Assert.That(saved.Status, Is.EqualTo(RoomStatus.Pending));
            Assert.That(saved.IsVerified, Is.False);
            Assert.That(saved.ImageUrls, Is.EqualTo(created.ImageUrls));
            Assert.That(warmedQuery!.Items.Select(item => item.Id), Does.Not.Contain(created.ListingId));
            Assert.That(freshQuery!.Items.Select(item => item.Id), Does.Not.Contain(created.ListingId));
        });
    }

    [Test]
    public async Task UnpublishListing_OwnerRequest_PreservesImagesAndRemovesListingFromPublicSearch()
    {
        var landlord = await RegisterAndLoginAsync("LANDLORD");
        var created = await CreateListingAsync(landlord.AccessToken);
        await PublishListingForTestAsync(created);
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

    private async Task PublishListingForTestAsync(ListingWriteResponse created)
    {
        var room = await _factory.RoomRepository.GetByIdAsync(created.ListingId);
        Assert.That(room, Is.Not.Null);
        room!.Status = RoomStatus.Available;
        room.IsVerified = true;
        _factory.RoomRepository.Upsert(room);

        await _factory.RoomListingRepository.AddAsync(new RoomListing(
            room.Id, room.Title, room.Description, new Money(room.Price, "VND"), room.Area,
            room.Address, new GeoLocation(21.5853, 105.8075), ListingRoomStatus.Available,
            VerificationStatus.Verified, "campus-ictu", room.ThumbnailUrl));
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
