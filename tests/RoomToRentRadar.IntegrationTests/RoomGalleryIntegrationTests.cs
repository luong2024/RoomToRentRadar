using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;
using SkiaSharp;
using RoomDetailsStatus = RoomToRentRadar.Domain.Entities.RoomStatus;

namespace RoomToRentRadar.IntegrationTests;

[TestFixture]
public class RoomGalleryIntegrationTests
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

        _factory.RoomRepository.Upsert(new Room
        {
            Id = "room-gallery-001",
            Title = "ICTU Studio Near Campus",
            Description = "Bright student room",
            Price = 2_000_000m,
            Area = 22,
            Address = "Z115 Street, Thai Nguyen",
            RoomType = "Private",
            Status = RoomDetailsStatus.Available,
            IsVerified = true
        });
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetGallery_AC4_And_AC3_EmptyGalleryReturns200WithCacheHeadersAndSupports304ETag()
    {
        var firstResponse = await _client.GetAsync("/api/v1/rooms/room-gallery-001/gallery");

        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var gallery = await firstResponse.Content.ReadFromJsonAsync<RoomGalleryResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(gallery, Is.Not.Null);
            Assert.That(gallery!.RoomId, Is.EqualTo("room-gallery-001"));
            Assert.That(gallery.HasPhotos, Is.False);
            Assert.That(gallery.TotalCount, Is.EqualTo(0));
            Assert.That(gallery.Photos, Is.Empty);
            Assert.That(firstResponse.Headers.CacheControl?.Public, Is.True);
            Assert.That(firstResponse.Headers.ETag, Is.Not.Null);
        });

        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rooms/room-gallery-001/gallery");
        conditionalRequest.Headers.IfNoneMatch.Add(firstResponse.Headers.ETag!);
        var notModifiedResponse = await _client.SendAsync(conditionalRequest);

        Assert.That(notModifiedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
    }

    [Test]
    public async Task UploadPhoto_Rbac_RejectsAnonymousWith401AndStudentWith403()
    {
        var jpegBytes = CreateTestJpeg(400, 300);

        using var anonForm = CreateUploadForm(jpegBytes, "room.jpg", "image/jpeg");
        var anonRes = await _client.PostAsync("/api/v1/rooms/room-gallery-001/gallery/photos", anonForm);
        Assert.That(anonRes.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var studentToken = await RegisterAndLoginAsync("student-gallery@ictu.edu.vn", "STUDENT");
        using var studentReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rooms/room-gallery-001/gallery/photos")
        {
            Content = CreateUploadForm(jpegBytes, "room.jpg", "image/jpeg")
        };
        studentReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var studentRes = await _client.SendAsync(studentReq);
        Assert.That(studentRes.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task FullGalleryLifecycle_UploadCompressReorderAndDelete_SucceedsEndToEnd()
    {
        var landlordSession = await RegisterAndLoginSessionAsync("landlord-gallery@ictu.edu.vn", "LANDLORD");
        AssignRoomOwner("room-gallery-001", landlordSession.UserId);
        var landlordToken = landlordSession.AccessToken;
        var standardJpeg = CreateTestJpeg(2200, 1400);
        var panoramaJpeg = CreateTestJpeg(2000, 1000);

        // 1. Upload standard photo
        using var upload1Req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rooms/room-gallery-001/gallery/photos")
        {
            Content = CreateUploadForm(standardJpeg, "living.jpg", "image/jpeg", is360: false, caption: "Living area")
        };
        upload1Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordToken);
        var upload1Res = await _client.SendAsync(upload1Req);
        Assert.That(upload1Res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var photo1 = await upload1Res.Content.ReadFromJsonAsync<RoomPhotoDto>();

        // 2. Upload 360° panorama photo (AC2)
        using var upload2Req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rooms/room-gallery-001/gallery/photos")
        {
            Content = CreateUploadForm(panoramaJpeg, "pano360.jpg", "image/jpeg", is360: true, caption: "360 Panorama")
        };
        upload2Req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordToken);
        var upload2Res = await _client.SendAsync(upload2Req);
        Assert.That(upload2Res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var photo2 = await upload2Res.Content.ReadFromJsonAsync<RoomPhotoDto>();

        Assert.Multiple(() =>
        {
            Assert.That(photo1, Is.Not.Null);
            Assert.That(photo1!.Width, Is.EqualTo(1920));
            Assert.That(photo1.IsCover, Is.True);
            Assert.That(photo1.ThumbnailUrl, Does.Contain("_thumb.jpg"));

            Assert.That(photo2, Is.Not.Null);
            Assert.That(photo2!.Is360, Is.True);
            Assert.That(photo2.Width, Is.EqualTo(2000));
        });

        // 3. Reorder photos and change cover
        using var reorderReq = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/rooms/room-gallery-001/gallery/reorder")
        {
            Content = JsonContent.Create(new ReorderRoomPhotosRequest([photo2!.Id, photo1!.Id], photo2.Id))
        };
        reorderReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordToken);
        var reorderRes = await _client.SendAsync(reorderReq);
        Assert.That(reorderRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var reordered = await reorderRes.Content.ReadFromJsonAsync<RoomGalleryResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(reordered!.TotalCount, Is.EqualTo(2));
            Assert.That(reordered.Has360Photos, Is.True);
            Assert.That(reordered.Photos[0].Id, Is.EqualTo(photo2.Id));
            Assert.That(reordered.Photos[0].IsCover, Is.True);
        });

        // 4. Delete photo2 -> photo1 becomes cover
        using var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/rooms/room-gallery-001/gallery/photos/{photo2.Id}");
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", landlordToken);
        var deleteRes = await _client.SendAsync(deleteReq);
        Assert.That(deleteRes.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var finalGallery = await _client.GetFromJsonAsync<RoomGalleryResponse>("/api/v1/rooms/room-gallery-001/gallery");
        Assert.Multiple(() =>
        {
            Assert.That(finalGallery!.TotalCount, Is.EqualTo(1));
            Assert.That(finalGallery.Photos[0].Id, Is.EqualTo(photo1.Id));
            Assert.That(finalGallery.Photos[0].IsCover, Is.True);
        });
    }

    [Test]
    public async Task OwnershipAuthorization_RejectsOtherLandlordWith403AndAllowsAdmin()
    {
        var ownerSession = await RegisterAndLoginSessionAsync("owner-landlord@ictu.edu.vn", "LANDLORD");
        var otherLandlordSession = await RegisterAndLoginSessionAsync("other-landlord@ictu.edu.vn", "LANDLORD");
        var adminSession = await ProvisionAdminAndLoginAsync("admin-gallery@roomtorentradar.local", "AdminPass123!");

        AssignRoomOwner("room-gallery-001", ownerSession.UserId);
        var jpegBytes = CreateTestJpeg(800, 600);

        // Owner uploads initial photo
        using var ownerUploadReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rooms/room-gallery-001/gallery/photos")
        {
            Content = CreateUploadForm(jpegBytes, "owner.jpg", "image/jpeg")
        };
        ownerUploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerSession.AccessToken);
        var ownerUploadRes = await _client.SendAsync(ownerUploadReq);
        Assert.That(ownerUploadRes.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var ownerPhoto = (await ownerUploadRes.Content.ReadFromJsonAsync<RoomPhotoDto>())!;

        // Other landlord cannot upload, reorder, or delete
        using var otherUploadReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rooms/room-gallery-001/gallery/photos")
        {
            Content = CreateUploadForm(jpegBytes, "intruder.jpg", "image/jpeg")
        };
        otherUploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherLandlordSession.AccessToken);
        var otherUploadRes = await _client.SendAsync(otherUploadReq);

        using var otherReorderReq = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/rooms/room-gallery-001/gallery/reorder")
        {
            Content = JsonContent.Create(new ReorderRoomPhotosRequest([ownerPhoto.Id], ownerPhoto.Id))
        };
        otherReorderReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherLandlordSession.AccessToken);
        var otherReorderRes = await _client.SendAsync(otherReorderReq);

        using var otherDeleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/rooms/room-gallery-001/gallery/photos/{ownerPhoto.Id}");
        otherDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherLandlordSession.AccessToken);
        var otherDeleteRes = await _client.SendAsync(otherDeleteReq);

        Assert.Multiple(() =>
        {
            Assert.That(otherUploadRes.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(otherReorderRes.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(otherDeleteRes.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });

        // Admin retains access to upload, reorder, and delete on any room
        using var adminUploadReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rooms/room-gallery-001/gallery/photos")
        {
            Content = CreateUploadForm(jpegBytes, "admin.jpg", "image/jpeg")
        };
        adminUploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var adminUploadRes = await _client.SendAsync(adminUploadReq);
        Assert.That(adminUploadRes.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var adminPhoto = (await adminUploadRes.Content.ReadFromJsonAsync<RoomPhotoDto>())!;

        using var adminReorderReq = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/rooms/room-gallery-001/gallery/reorder")
        {
            Content = JsonContent.Create(new ReorderRoomPhotosRequest([adminPhoto.Id, ownerPhoto.Id], adminPhoto.Id))
        };
        adminReorderReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var adminReorderRes = await _client.SendAsync(adminReorderReq);
        Assert.That(adminReorderRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var adminDeleteReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/rooms/room-gallery-001/gallery/photos/{adminPhoto.Id}");
        adminDeleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
        var adminDeleteRes = await _client.SendAsync(adminDeleteReq);
        Assert.That(adminDeleteRes.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    private void AssignRoomOwner(string roomId, Guid landlordId)
    {
        var room = _factory.RoomRepository.GetByIdAsync(roomId).GetAwaiter().GetResult()!;
        room.LandlordId = landlordId;
        _factory.RoomRepository.Upsert(room);
    }

    private async Task<string> RegisterAndLoginAsync(string email, string role)
    {
        var session = await RegisterAndLoginSessionAsync(email, role);
        return session.AccessToken;
    }

    private async Task<AuthSessionResponse> RegisterAndLoginSessionAsync(string email, string role)
    {
        var registerRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterUserRequest(email, "StrongPass123!", role));
        Assert.That(registerRes.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginWithEmailRequest(email, "StrongPass123!"));
        Assert.That(loginRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    private async Task<AuthSessionResponse> ProvisionAdminAndLoginAsync(string email, string password)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var admin = User.ProvisionAdminUser(
                EmailAddress.Create(email),
                passwordHasher.HashPassword(password),
                DateTimeOffset.UtcNow);

            await userRepository.AddAsync(admin);
            await unitOfWork.SaveChangesAsync();
        }

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginWithEmailRequest(email, password));
        Assert.That(loginRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    private static MultipartFormDataContent CreateUploadForm(
        byte[] bytes,
        string fileName,
        string contentType,
        bool is360 = false,
        string? caption = null)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent(is360.ToString().ToLowerInvariant()), "Is360");
        if (!string.IsNullOrWhiteSpace(caption))
        {
            form.Add(new StringContent(caption), "Caption");
        }

        return form;
    }

    private static byte[] CreateTestJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }
}
