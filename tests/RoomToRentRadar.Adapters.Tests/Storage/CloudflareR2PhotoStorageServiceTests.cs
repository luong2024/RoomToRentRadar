using System.Net;
using RoomToRentRadar.Adapters.Outbound.Storage;
using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Adapters.Tests.Storage;

[TestFixture]
public class CloudflareR2PhotoStorageServiceTests
{
    [Test]
    public async Task UploadAsync_SignsAwsSigV4HeadersAndUploadsMainAndThumbnailWithCacheControl()
    {
        var capturedRequests = new List<HttpRequestMessage>();
        var handler = new CapturingHttpMessageHandler(req =>
        {
            capturedRequests.Add(req);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var options = new PhotoStorageOptions
        {
            Provider = "CloudflareR2",
            AccountId = "acc123456",
            BucketName = "room-photos",
            AccessKeyId = "AKIA_TEST_KEY",
            SecretAccessKey = "SUPER_SECRET_TEST_KEY",
            PublicBaseUrl = "https://pub-demo.r2.dev/"
        };

        var service = new CloudflareR2PhotoStorageService(new HttpClient(handler), options);
        var processed = new ProcessedImageResult(
            CompressedBytes: [1, 2, 3, 4],
            ThumbnailBytes: [5, 6],
            ContentType: "image/jpeg",
            FileExtension: ".jpg",
            Width: 1920,
            Height: 1080);

        var result = await service.UploadAsync("room-ictu-001", "photo-01", processed);

        Assert.Multiple(() =>
        {
            Assert.That(capturedRequests, Has.Count.EqualTo(2));
            Assert.That(capturedRequests[0].Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(capturedRequests[0].RequestUri!.ToString(),
                Is.EqualTo("https://acc123456.r2.cloudflarestorage.com/room-photos/rooms/room-ictu-001/photo-01.jpg"));
            Assert.That(capturedRequests[0].Headers.TryGetValues("Authorization", out var authValues), Is.True);
            Assert.That(authValues!.Single(), Does.StartWith("AWS4-HMAC-SHA256 Credential=AKIA_TEST_KEY/"));
            Assert.That(capturedRequests[0].Headers.Contains("x-amz-date"), Is.True);
            Assert.That(capturedRequests[0].Headers.Contains("x-amz-content-sha256"), Is.True);
            Assert.That(capturedRequests[0].Headers.TryGetValues("Cache-Control", out var ccValues), Is.True);
            Assert.That(ccValues!.Single(), Does.Contain("max-age=31536000"));

            Assert.That(result.Url, Is.EqualTo("https://pub-demo.r2.dev/rooms/room-ictu-001/photo-01.jpg"));
            Assert.That(result.ThumbnailUrl, Is.EqualTo("https://pub-demo.r2.dev/rooms/room-ictu-001/photo-01_thumb.jpg"));
        });
    }

    [Test]
    public async Task DeleteAsync_SendsSignedDeleteRequestsForBothMainAndThumbnail()
    {
        var capturedRequests = new List<HttpRequestMessage>();
        var handler = new CapturingHttpMessageHandler(req =>
        {
            capturedRequests.Add(req);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var options = new PhotoStorageOptions
        {
            Provider = "CloudflareR2",
            AccountId = "acc123456",
            BucketName = "room-photos",
            AccessKeyId = "AKIA_TEST_KEY",
            SecretAccessKey = "SUPER_SECRET_TEST_KEY",
            PublicBaseUrl = "https://pub-demo.r2.dev"
        };

        var service = new CloudflareR2PhotoStorageService(new HttpClient(handler), options);

        await service.DeleteAsync("rooms/room-1/p1.jpg", "rooms/room-1/p1_thumb.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(capturedRequests, Has.Count.EqualTo(2));
            Assert.That(capturedRequests.All(r => r.Method == HttpMethod.Delete), Is.True);
            Assert.That(capturedRequests.All(r => r.Headers.Contains("Authorization")), Is.True);
        });
    }

    private sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
