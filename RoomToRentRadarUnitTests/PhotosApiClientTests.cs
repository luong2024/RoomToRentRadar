using System.Net;
using System.Text;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class PhotosApiClientTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");
    private static readonly Guid CoverPhotoId = Guid.Parse("0b0f5f18-1e73-4f63-9a1c-2c4f0b7d0001");
    private static readonly Guid PanoramaPhotoId = Guid.Parse("0b0f5f18-1e73-4f63-9a1c-2c4f0b7d0002");

    private const string PhotosJson = """
        [
          {
            "id": "0b0f5f18-1e73-4f63-9a1c-2c4f0b7d0001",
            "listingId": "2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234",
            "url": "/media/room-cover.jpg",
            "thumbUrl": "/media/room-cover-thumb.jpg",
            "width": 1600,
            "height": 900,
            "kind": "photo",
            "alt": "Phong khach",
            "sortOrder": 0,
            "isCover": true
          },
          {
            "id": "0b0f5f18-1e73-4f63-9a1c-2c4f0b7d0002",
            "listingId": "2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234",
            "url": "/media/room-panorama.jpg",
            "thumbUrl": null,
            "width": 4000,
            "height": 1000,
            "kind": "panorama",
            "alt": null,
            "sortOrder": 1,
            "isCover": false
          }
        ]
        """;

    [Test]
    public async Task GetPhotosAsync_WhenTheEndpointReturnsPhotos_ReadsTheCamelCaseArray()
    {
        var handler = new RecordingHandler(_ => JsonResponse(PhotosJson));
        var client = CreateClient(handler);

        var result = await client.GetPhotosAsync(ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Methods, Has.Count.EqualTo(1));
            Assert.That(handler.Methods[0], Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.RequestUris, Has.Count.EqualTo(1));
            Assert.That(handler.RequestUris[0].AbsolutePath, Is.EqualTo($"/api/v1/listings/{ListingId}/photos"));
            Assert.That(result.Outcome, Is.EqualTo(ListingPhotosFetchOutcome.Found));
            Assert.That(result.Photos, Has.Count.EqualTo(2));
        });

        var cover = result.Photos[0];
        var panorama = result.Photos[1];
        Assert.Multiple(() =>
        {
            Assert.That(cover.Id, Is.EqualTo(CoverPhotoId));
            Assert.That(cover.ListingId, Is.EqualTo(ListingId));
            Assert.That(cover.Url, Is.EqualTo("/media/room-cover.jpg"));
            Assert.That(cover.ThumbUrl, Is.EqualTo("/media/room-cover-thumb.jpg"));
            Assert.That(cover.Width, Is.EqualTo(1600));
            Assert.That(cover.Height, Is.EqualTo(900));
            Assert.That(cover.Kind, Is.EqualTo(ListingPhotoKind.Photo));
            Assert.That(cover.Alt, Is.EqualTo("Phong khach"));
            Assert.That(cover.SortOrder, Is.Zero);
            Assert.That(cover.IsCover, Is.True);
            Assert.That(panorama.Id, Is.EqualTo(PanoramaPhotoId));
            Assert.That(panorama.Kind, Is.EqualTo(ListingPhotoKind.Panorama));
            Assert.That(panorama.ThumbUrl, Is.Null);
            Assert.That(panorama.Alt, Is.Null);
            Assert.That(panorama.SortOrder, Is.EqualTo(1));
            Assert.That(panorama.IsCover, Is.False);
        });
    }

    [Test]
    public async Task GetPhotosAsync_WhenTheEndpointReturnsAnEmptyArray_ReportsFound()
    {
        var handler = new RecordingHandler(_ => JsonResponse("[]"));
        var client = CreateClient(handler);

        var result = await client.GetPhotosAsync(ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ListingPhotosFetchOutcome.Found));
            Assert.That(result.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task GetPhotosAsync_WhenTheListingIsMissing_ReportsNotFound()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetPhotosAsync(ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ListingPhotosFetchOutcome.NotFound));
            Assert.That(result.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task GetPhotosAsync_WhenTheListingIsUnavailable_ReportsUnavailable()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Gone));
        var client = CreateClient(handler);

        var result = await client.GetPhotosAsync(ListingId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ListingPhotosFetchOutcome.Unavailable));
            Assert.That(result.Photos, Is.Empty);
        });
    }

    [Test]
    public void GetPhotosAsync_WhenTheServerFails_Throws()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        });
        var client = CreateClient(handler);

        var exception = Assert.ThrowsAsync<HttpRequestException>(() => client.GetPhotosAsync(ListingId));

        Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task GetMediaBytesAsync_WhenTheFileExists_ReturnsTheBytesFromTheAbsoluteUrl()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var client = CreateClient(handler);

        var result = await client.GetMediaBytesAsync("http://localhost:5080/media/room-cover.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Methods[0], Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.RequestUris[0].AbsoluteUri, Is.EqualTo("http://localhost:5080/media/room-cover.jpg"));
            Assert.That(result, Is.EqualTo(bytes));
        });
    }

    [Test]
    public async Task GetMediaBytesAsync_WhenTheFileIsMissing_ReturnsNull()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetMediaBytesAsync("http://localhost:5080/media/missing.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(handler.RequestUris[0].AbsoluteUri, Is.EqualTo("http://localhost:5080/media/missing.jpg"));
        });
    }

    private static PhotosApiClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") });

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = new();

        public List<Uri> RequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            RequestUris.Add(request.RequestUri!);

            return Task.FromResult(respond(request));
        }
    }
}
