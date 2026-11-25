using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingManagementClientTests
{
    [Test]
    public async Task CreateAsync_SendsInvariantMultipartFieldsAndAllPhotos_AndReadsPendingReview()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var fields = new Dictionary<string, string>();
            var photoBytes = new List<byte[]>();
            var handler = new StubHandler(async request =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/listings"));
                var multipart = (MultipartFormDataContent)request.Content!;
                foreach (var part in multipart)
                {
                    var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                    if (name == "photos")
                    {
                        Assert.That(part.Headers.ContentType!.MediaType, Is.EqualTo("image/png"));
                        photoBytes.Add(await part.ReadAsByteArrayAsync());
                    }
                    else
                    {
                        fields[name] = await part.ReadAsStringAsync();
                    }
                }
                return JsonResponse(Listing());
            });
            var form = Form(Enumerable.Range(0, 3).Select(index => new ListingPhotoUpload(
                $"photo-{index}.png", "image/png", () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])))).ToArray());

            var result = await Client(handler).CreateAsync(form);

            Assert.Multiple(() =>
            {
                Assert.That(fields["title"], Is.EqualTo("Room"));
                Assert.That(fields["address"], Is.EqualTo("10 Campus Road"));
                Assert.That(fields["price"], Is.EqualTo("2500000"));
                Assert.That(fields["deposit"], Is.EqualTo("0"));
                Assert.That(fields["area_m2"], Is.EqualTo("20.5"));
                Assert.That(fields["room_type"], Is.EqualTo("studio"));
                Assert.That(fields["lat"], Is.EqualTo("10.75"));
                Assert.That(fields["lng"], Is.EqualTo("106.7"));
                Assert.That(fields["description"], Is.EqualTo("Near campus"));
                Assert.That(photoBytes, Has.Count.EqualTo(3));
                Assert.That(photoBytes.All(bytes => bytes.SequenceEqual(new byte[] { 1, 2, 3 })), Is.True);
                Assert.That(result.Status, Is.EqualTo(ListingStatus.PendingReview));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task CreateAsync_RetryAfterFailedUpload_ReopensAndDisposesEachPhotoStream()
    {
        var streams = new List<MemoryStream>();
        var calls = 0;
        var handler = new StubHandler(async request =>
        {
            await request.Content!.ReadAsByteArrayAsync();
            return ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("Unavailable") }
                : JsonResponse(Listing());
        });
        var client = Client(handler);
        var form = Form([new ListingPhotoUpload("room.png", "image/png", () =>
        {
            var stream = new MemoryStream([1, 2, 3]);
            streams.Add(stream);
            return Task.FromResult<Stream>(stream);
        })]);

        Assert.ThrowsAsync<ListingManagementException>(() => client.CreateAsync(form));
        var result = await client.CreateAsync(form);

        Assert.Multiple(() =>
        {
            Assert.That(streams, Has.Count.EqualTo(2));
            Assert.That(streams.All(stream => !stream.CanRead), Is.True);
            Assert.That(result.Status, Is.EqualTo(ListingStatus.PendingReview));
        });
    }

    [Test]
    public void CreateAsync_ValidationProblem_PreservesFieldErrorsAndCode()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""
                {"title":"Invalid listing","detail":"Correct the highlighted fields.","code":"invalid_listing",
                 "errors":{"title":["Title is required."],"deposit":["Deposit is required."],"area_m2":["Area must be positive."]}}
                """, Encoding.UTF8, "application/problem+json")
        }));

        var exception = Assert.ThrowsAsync<ListingManagementException>(() => Client(handler).CreateAsync(Form()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(exception.Code, Is.EqualTo("invalid_listing"));
            Assert.That(exception.Message, Is.EqualTo("Correct the highlighted fields."));
            Assert.That(exception.Errors["title"], Is.EqualTo(new[] { "Title is required." }));
            Assert.That(exception.Errors["deposit"], Is.EqualTo(new[] { "Deposit is required." }));
            Assert.That(exception.Errors["area_m2"], Is.EqualTo(new[] { "Area must be positive." }));
        });
    }

    [Test]
    public async Task UpdateAsync_UsesPutWithoutReuploadingExistingPhotos()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/listings/{id}"));
            Assert.That(((MultipartFormDataContent)request.Content!).Any(part =>
                part.Headers.ContentDisposition!.Name!.Trim('"') == "photos"), Is.False);
            return Task.FromResult(JsonResponse(Listing(id)));
        });

        var result = await Client(handler).UpdateAsync(id, Form());

        Assert.That(result.Id, Is.EqualTo(id));
    }

    [Test]
    public async Task GetManagedAsync_UsesOwnerEndpointForPendingListing()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/listings/{id}/manage"));
            return Task.FromResult(JsonResponse(Listing(id)));
        });

        var result = await Client(handler).GetManagedAsync(id);

        Assert.That(result.Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public async Task PublishAsync_PostsAndReturnsPendingReviewStatus()
    {
        // Arrange
        var id = Guid.NewGuid();
        HttpMethod? method = null;
        string? path = null;
        var handler = new StubHandler(request =>
        {
            method = request.Method;
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(JsonResponse(Listing(id) with { Status = ListingStatus.PendingReview }));
        });

        // Act
        var result = await Client(handler).PublishAsync(id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(method, Is.EqualTo(HttpMethod.Post));
            Assert.That(path, Is.EqualTo($"/api/v1/listings/{id}/publish"));
            Assert.That(result.Id, Is.EqualTo(id));
            Assert.That(result.Status, Is.EqualTo(ListingStatus.PendingReview));
        });
    }

    [Test]
    public async Task UnpublishAsync_PostsAndReturnsHiddenStatus()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/listings/{id}/unpublish"));
            return Task.FromResult(JsonResponse(Listing(id) with { Status = ListingStatus.Hidden }));
        });

        var result = await Client(handler).UnpublishAsync(id);

        Assert.That(result.Status, Is.EqualTo(ListingStatus.Hidden));
    }

    [Test]
    public async Task DeleteAsync_NoContentResponse_CompletesWithoutDeserializingBody()
    {
        // Arrange
        var id = Guid.NewGuid();
        HttpMethod? method = null;
        string? path = null;
        var handler = new StubHandler(request =>
        {
            method = request.Method;
            path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        // Act
        await Client(handler).DeleteAsync(id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(method, Is.EqualTo(HttpMethod.Delete));
            Assert.That(path, Is.EqualTo($"/api/v1/listings/{id}"));
        });
    }

    [Test]
    public void DeleteAsync_NotFoundProblem_ThrowsListingManagementExceptionWithCodeAndMessage()
    {
        // Arrange
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""
                {"title":"Listing not found","detail":"Tin đăng không tồn tại.","code":"listing_not_found"}
                """, Encoding.UTF8, "application/problem+json")
        }));

        // Act
        var exception = Assert.ThrowsAsync<ListingManagementException>(() => Client(handler).DeleteAsync(id))!;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(exception.Code, Is.EqualTo("listing_not_found"));
            Assert.That(exception.Message, Is.EqualTo("Tin đăng không tồn tại."));
            Assert.That(exception.Errors, Is.Empty);
        });
    }

    [Test]
    public async Task GetMineAsync_LoadsAllOwnerPagesIncludingPendingAndHidden()
    {
        var requests = new List<string>();
        var firstPage = Enumerable.Range(0, 100).Select(_ => Listing()).ToArray();
        var hidden = Listing() with { Status = ListingStatus.Hidden };
        var handler = new StubHandler(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            requests.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(requests.Count == 1 ? JsonResponse(firstPage) : JsonResponse(new[] { hidden }));
        });

        var result = await Client(handler).GetMineAsync();

        Assert.Multiple(() =>
        {
            Assert.That(requests, Is.EqualTo(new[]
            {
                "/api/v1/listings/mine?limit=100&offset=0",
                "/api/v1/listings/mine?limit=100&offset=100"
            }));
            Assert.That(result, Has.Count.EqualTo(101));
            Assert.That(result[^1].Status, Is.EqualTo(ListingStatus.Hidden));
        });
    }

    [Test]
    public void GetMineAsync_Unauthorized_ReportsAuthenticationFailure()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"title":"Sign in required"}""", Encoding.UTF8, "application/problem+json")
        }));

        var exception = Assert.ThrowsAsync<ListingManagementException>(() => Client(handler).GetMineAsync())!;

        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UpdateHouseRulesAsync_PutsCamelCaseJsonAndReadsVersionedRules()
    {
        var id = Guid.NewGuid();
        string? body = null;
        var handler = new StubHandler(async request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/listings/{id}/house-rules"));
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse(new HouseRulesDto(
                "22:00 - 06:00",
                "Guests leave before 22:00",
                "Cook in the shared kitchen",
                "Park on the ground floor",
                4,
                new DateTime(2026, 11, 19, 8, 30, 0, DateTimeKind.Utc)));
        });

        var result = await Client(handler).UpdateHouseRulesAsync(id, new UpdateHouseRulesRequest
        {
            QuietHours = "22:00 - 06:00",
            GuestPolicy = "Guests leave before 22:00",
            CookingRules = "Cook in the shared kitchen",
            ParkingRules = "Park on the ground floor"
        });

        Assert.Multiple(() =>
        {
            using var document = JsonDocument.Parse(body!);
            var root = document.RootElement;
            Assert.That(root.GetProperty("quietHours").GetString(), Is.EqualTo("22:00 - 06:00"));
            Assert.That(root.GetProperty("guestPolicy").GetString(), Is.EqualTo("Guests leave before 22:00"));
            Assert.That(root.GetProperty("cookingRules").GetString(), Is.EqualTo("Cook in the shared kitchen"));
            Assert.That(root.GetProperty("parkingRules").GetString(), Is.EqualTo("Park on the ground floor"));
            Assert.That(result.Version, Is.EqualTo(4));
            Assert.That(result.ParkingRules, Is.EqualTo("Park on the ground floor"));
        });
    }

    [Test]
    public void UpdateHouseRulesAsync_ValidationProblem_PreservesFieldErrors()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""
                {"title":"Invalid house rules","detail":"Correct the highlighted fields.","code":"invalid_house_rules",
                 "errors":{"quiet_hours":["This field is required."],"guest_policy":["This field is required."]}}
                """, Encoding.UTF8, "application/problem+json")
        }));

        var exception = Assert.ThrowsAsync<ListingManagementException>(() => Client(handler).UpdateHouseRulesAsync(
            Guid.NewGuid(), new UpdateHouseRulesRequest()))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(exception.Errors["quiet_hours"], Is.EqualTo(new[] { "This field is required." }));
            Assert.That(exception.Errors["guest_policy"], Is.EqualTo(new[] { "This field is required." }));
        });
    }

    private static ListingFormSubmission Form(IReadOnlyList<ListingPhotoUpload>? photos = null)
        => new(" Room ", " 10 Campus Road ", 2_500_000, 0, 20.5, RoomType.Studio, 10.75, 106.7,
            photos ?? [], " Near campus ");

    private static ManagedListingDto Listing(Guid? id = null)
        => new(id ?? Guid.NewGuid(), "Room", "Near campus", 2_500_000, 0, 20.5, RoomType.Studio,
            1, "10 Campus Road", 10.75, 106.7, ListingStatus.PendingReview,
            new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc), [], [], "Landlord", []);

    private static ListingManagementClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, ApiJson.Options), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request);
    }
}
