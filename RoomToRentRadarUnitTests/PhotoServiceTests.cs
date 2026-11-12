using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Photos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class PhotoServiceTests
{
    private sealed class FakeStorage : IPhotoStorage
    {
        public Dictionary<string, byte[]> Files = new();
        public Task<string> SaveAsync(string listingId, string fileName, byte[] content, CancellationToken ct)
        { Files[fileName] = content; return Task.FromResult($"/media/listings/{listingId}/{fileName}"); }
        public Task DeleteAsync(string listingId, string fileName, CancellationToken ct)
        { Files.Remove(fileName); return Task.CompletedTask; }
    }

    private const string Room = "room-2026-001";

    private static (PhotoService Svc, FakeStorage Storage) Create()
    {
        var storage = new FakeStorage();
        var opts = Options.Create(new PhotoOptions());
        return (new PhotoService(new InMemoryPhotoRepository(), storage, new ImageSharpProcessor(opts), opts), storage);
    }

    private static byte[] MakePng(int w, int h)
    {
        using var img = new Image<Rgba32>(w, h, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static PhotoUploadItem Item(string name, byte[] data, bool is360 = false) =>
        new(name, data.Length, new MemoryStream(data), is360);

    [Fact]
    public async Task Upload_ValidPng_CompressesToJpegAndStoresMetadata()
    {
        var (svc, storage) = Create();
        var png = MakePng(3000, 2000);

        var res = await svc.UploadAsync(Room, [Item("a.png", png)], default);

        Xunit.Assert.Equal(1, res.UploadedCount);
        Xunit.Assert.Equal(1920, res.Photos[0].Width);
        Xunit.Assert.Equal(2, storage.Files.Count);
        Xunit.Assert.EndsWith(".jpg", res.Photos[0].Url);

        var gallery = await svc.GetGalleryAsync(Room, default);
        Xunit.Assert.True(gallery.TotalPhotos >= 1);
    }

    [Fact]
    public async Task Upload_360Photo_RequiresTwoToOneRatio()
    {
        var (svc, _) = Create();
        await Xunit.Assert.ThrowsAsync<PhotoValidationException>(
            () => svc.UploadAsync("room-2026-002", [Item("p.png", MakePng(1000, 1000), true)], default));

        var ok = await svc.UploadAsync("room-2026-002", [Item("p.png", MakePng(2000, 1000), true)], default);
        Xunit.Assert.True(ok.Photos[0].Is360);
    }

    [Fact]
    public async Task Upload_WrongExtension_Rejected()
    {
        var (svc, _) = Create();
        await Xunit.Assert.ThrowsAsync<PhotoValidationException>(
            () => svc.UploadAsync(Room, [Item("a.gif", MakePng(10, 10))], default));
    }

    [Fact]
    public async Task Upload_FakeImageContent_Rejected()
    {
        var (svc, _) = Create();
        var fake = System.Text.Encoding.UTF8.GetBytes("this is not an image");
        await Xunit.Assert.ThrowsAsync<PhotoValidationException>(
            () => svc.UploadAsync(Room, [Item("evil.jpg", fake)], default));
    }

    [Fact]
    public async Task Upload_TooLarge_Rejected()
    {
        var (svc, _) = Create();
        var item = new PhotoUploadItem("big.jpg", 11L * 1024 * 1024, new MemoryStream(), false);
        await Xunit.Assert.ThrowsAsync<PhotoValidationException>(() => svc.UploadAsync(Room, [item], default));
    }

    [Fact]
    public async Task Upload_UnknownListing_ThrowsNotFound()
    {
        var (svc, _) = Create();
        await Xunit.Assert.ThrowsAsync<ListingNotFoundException>(
            () => svc.UploadAsync("room-does-not-exist", [Item("a.png", MakePng(10, 10))], default));
        await Xunit.Assert.ThrowsAsync<ListingNotFoundException>(() => svc.GetGalleryAsync("nope", default));
    }

    [Fact]
    public async Task Upload_PathTraversalId_Rejected()
    {
        var (svc, _) = Create();
        await Xunit.Assert.ThrowsAsync<PhotoValidationException>(
            () => svc.UploadAsync("..\\..\\x", [Item("a.png", MakePng(10, 10))], default));
    }

    [Fact]
    public async Task Upload_InvalidBatch_SavesNothing()
    {
        var (svc, storage) = Create();
        var good = Item("a.png", MakePng(100, 100));
        var bad = Item("b.jpg", System.Text.Encoding.UTF8.GetBytes("junk"));
        await Xunit.Assert.ThrowsAsync<PhotoValidationException>(() => svc.UploadAsync("room-2026-003", [good, bad], default));
        Xunit.Assert.Empty(storage.Files);
    }
}
