using RoomToRentRadar.Adapters.Outbound.Storage;
using RoomToRentRadar.Domain.Exceptions;
using SkiaSharp;

namespace RoomToRentRadar.Adapters.Tests.Storage;

[TestFixture]
public class SkiaImageCompressionServiceTests
{
    private SkiaImageCompressionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new SkiaImageCompressionService();
    }

    [Test]
    public async Task ProcessAsync_StandardLargePng_ResizesTo1920AndCreatesLightweightThumbnail()
    {
        var rawPng = CreateSampleImageBytes(2400, 1600, SKEncodedImageFormat.Png);

        var result = await _service.ProcessAsync(rawPng, "image/png", is360: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(result.FileExtension, Is.EqualTo(".jpg"));
            Assert.That(result.Width, Is.EqualTo(1920));
            Assert.That(result.Height, Is.EqualTo(1280));
            Assert.That(result.CompressedBytes, Is.Not.Empty);
            Assert.That(result.ThumbnailBytes, Is.Not.Empty);
            Assert.That(result.ThumbnailBytes.Length, Is.LessThan(result.CompressedBytes.Length));
        });
    }

    [Test]
    public async Task ProcessAsync_Panorama360Image_PreservesHighResolutionUpTo4096Px()
    {
        var rawJpeg = CreateSampleImageBytes(3000, 1500, SKEncodedImageFormat.Jpeg);

        var result = await _service.ProcessAsync(rawJpeg, "image/jpeg", is360: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Width, Is.EqualTo(3000));
            Assert.That(result.Height, Is.EqualTo(1500));
            Assert.That(result.CompressedBytes, Is.Not.Empty);
            Assert.That(result.ThumbnailBytes, Is.Not.Empty);
        });
    }

    [Test]
    public void ProcessAsync_SpoofedNonImagePayloadWithJpgContentType_ThrowsInvalidRoomPhotoException()
    {
        var fakePayload = "MZ-this-is-not-a-real-jpeg-file"u8.ToArray();

        Assert.ThrowsAsync<InvalidRoomPhotoException>(() =>
            _service.ProcessAsync(fakePayload, "image/jpeg", is360: false));
    }

    private static byte[] CreateSampleImageBytes(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
            using var paint = new SKPaint { Color = SKColors.Orange, IsAntialias = true };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
            using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawRect(20, 20, width - 40, 120, textPaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return data.ToArray();
    }
}
