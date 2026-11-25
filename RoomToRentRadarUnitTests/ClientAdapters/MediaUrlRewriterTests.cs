using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Utilities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters;

/// <summary>T00.10 AC D5: photos of a local API load on an emulator or phone.</summary>
[TestFixture]
public class MediaUrlRewriterTests
{
    private static readonly Uri Emulator = new("http://10.0.2.2:5000");

    [TestCase("http://localhost:5000/media/seed/rooms/a-thumb.jpg", "http://10.0.2.2:5000/media/seed/rooms/a-thumb.jpg")]
    [TestCase("http://127.0.0.1:5000/media/rooms/x.jpg", "http://10.0.2.2:5000/media/rooms/x.jpg")]
    [TestCase("http://[::1]:5000/media/rooms/x.jpg", "http://10.0.2.2:5000/media/rooms/x.jpg")]
    [TestCase("http://localhost:5000/media/rooms/x.jpg?v=2", "http://10.0.2.2:5000/media/rooms/x.jpg?v=2")]
    public void LoopbackMediaUrl_MovesToTheApiAddress(string url, string expected)
    {
        Assert.That(MediaUrlRewriter.Rewrite(url, Emulator), Is.EqualTo(expected));
    }

    [Test]
    public void HttpsApiBase_AppliesItsSchemeAndDefaultPort()
    {
        var result = MediaUrlRewriter.Rewrite(
            "http://localhost:5000/media/rooms/x.jpg", new Uri("https://abc.devtunnels.ms"));

        Assert.That(result, Is.EqualTo("https://abc.devtunnels.ms/media/rooms/x.jpg"));
    }

    [TestCase("https://cdn.example.com/media/rooms/x.jpg")]
    [TestCase("http://localhost:5000/api/rooms")]
    [TestCase("/media/rooms/x.jpg")]
    [TestCase("file:///media/rooms/x.jpg")]
    [TestCase("not a url")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherUrls_AreUntouched(string? url)
    {
        Assert.That(MediaUrlRewriter.Rewrite(url, Emulator), Is.EqualTo(url));
    }

    [Test]
    public void NoBaseAddress_LeavesTheUrl()
    {
        const string url = "http://localhost:5000/media/rooms/x.jpg";

        Assert.That(MediaUrlRewriter.Rewrite(url, null), Is.EqualTo(url));
    }

    [Test]
    public async Task HttpRoomGateway_RewritesCoversInSearchAndSimilarResults()
    {
        var card = new RoomCardDto(
            "r1", "Room", 1_000_000, 20, RoomType.BoardingRoom, 0.5, true, ListingStatus.Available,
            "http://localhost:5000/media/seed/rooms/a-thumb.jpg", 21.5, 105.8, null, false, Furnishing.Basic);
        var stub = new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                request.RequestUri!.AbsolutePath.EndsWith("/similar", StringComparison.Ordinal)
                    ? JsonSerializer.Serialize(new[] { card }, ApiResponseReader.JsonOptions)
                    : JsonSerializer.Serialize(new PagedResult<RoomCardDto>([card], 1, 1, 20), ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        });
        using var client = new HttpClient(stub) { BaseAddress = Emulator };
        var gateway = new HttpRoomGateway(client);

        var page = await gateway.SearchRoomsAsync(new RoomSearchRequest());
        var similar = await gateway.GetSimilarRoomsAsync("r1");

        const string expected = "http://10.0.2.2:5000/media/seed/rooms/a-thumb.jpg";
        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Single().CoverThumbUrl, Is.EqualTo(expected));
            Assert.That(page.TotalCount, Is.EqualTo(1));
            Assert.That(similar.Single().CoverThumbUrl, Is.EqualTo(expected));
            Assert.That(similar.Single().Furnishing, Is.EqualTo(Furnishing.Basic));
        });
    }

    [Test]
    public async Task HttpRoomGateway_RewritesEveryDetailPhotoUrl()
    {
        const string local = "http://localhost:5000/media/seed/rooms/a";
        var detail = new RoomDetailDto(
            "r1", "Room", null, RoomType.BoardingRoom, ListingStatus.Available, true,
            1_000_000, 1_000_000, 20, 2, Furnishing.Basic, null,
            new AddressDto("12 Z115", "Quyết Thắng", "Thái Nguyên"),
            new GeoPointDto(21.5851, 105.8066),
            0.5,
            new UtilitiesView(3500, 100000, WaterPriceUnit.PerPerson, null, null, null, 1_450_000),
            [],
            new SafetyDto(0, false, []),
            null,
            [new PhotoDto("p1", PhotoKind.Standard, $"{local}-thumb.jpg", $"{local}-medium.jpg", $"{local}-medium.jpg", 1280, 853, 0)],
            new LandlordContactDto("l1", "Chủ trọ", true, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), null, null),
            false,
            null,
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(detail, ApiResponseReader.JsonOptions), Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(stub) { BaseAddress = Emulator };

        var result = await new HttpRoomGateway(client).GetRoomDetailAsync("r1");

        var photo = result.Photos.Single();
        Assert.That(
            new[] { photo.ThumbUrl, photo.MediumUrl, photo.OriginalUrl },
            Is.All.StartsWith("http://10.0.2.2:5000/media/seed/rooms/a-"));
    }
}
