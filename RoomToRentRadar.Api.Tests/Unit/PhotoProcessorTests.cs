using SkiaSharp;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Api.Tests.Infrastructure;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class PhotoProcessorTests
{
    private readonly PhotoProcessor _processor = new();

    [Test]
    public void TryProcess_Jpeg_ReturnsOrientedDimensionsAndJpegThumbnail()
    {
        var content = TestImageFactory.CreateJpeg(640, 480);

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.That(succeeded, Is.True);
        Assert.That(photo, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(photo!.Width, Is.EqualTo(640));
            Assert.That(photo.Height, Is.EqualTo(480));
            Assert.That(IsJpeg(photo.Original), Is.True);
            Assert.That(IsJpeg(photo.Thumbnail), Is.True);
        });

        var thumbnail = ReadDimensions(photo!.Thumbnail);
        Assert.Multiple(() =>
        {
            Assert.That(thumbnail.Width, Is.EqualTo(PhotoUploadLimits.MaxThumbnailEdge));
            Assert.That(
                Math.Max(thumbnail.Width, thumbnail.Height),
                Is.LessThanOrEqualTo(PhotoUploadLimits.MaxThumbnailEdge));
        });
    }

    [Test]
    public void TryProcess_Png_IsNormalizedToJpeg()
    {
        var content = TestImageFactory.CreatePng(800, 600);

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.That(succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(photo!.Width, Is.EqualTo(800));
            Assert.That(photo.Height, Is.EqualTo(600));
            Assert.That(IsJpeg(photo.Original), Is.True);
        });
    }

    [Test]
    public void TryProcess_Webp_IsNormalizedToJpeg()
    {
        var content = TestImageFactory.CreateWebp(800, 600);

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.That(succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(photo!.Width, Is.EqualTo(800));
            Assert.That(photo.Height, Is.EqualTo(600));
            Assert.That(IsJpeg(photo.Original), Is.True);
        });
    }

    [Test]
    public void TryProcess_PngWithTransparency_CompositesOnWhite()
    {
        var content = TestImageFactory.CreateTransparentPng(64, 64);

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.That(succeeded, Is.True);
        using var data = SKData.CreateCopy(photo!.Original);
        using var bitmap = SKBitmap.Decode(data);
        var corner = bitmap.GetPixel(0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(corner.Red, Is.GreaterThanOrEqualTo(250));
            Assert.That(corner.Green, Is.GreaterThanOrEqualTo(250));
            Assert.That(corner.Blue, Is.GreaterThanOrEqualTo(250));
        });
    }

    [Test]
    public void TryProcess_JpegWithRotatedExifOrigin_SwapsDimensionsAndRotatesPixels()
    {
        var content = TestImageFactory.CreateJpegWithOrientation(800, 600, 6);

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.That(succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(photo!.Width, Is.EqualTo(600));
            Assert.That(photo.Height, Is.EqualTo(800));
        });

        var thumbnail = ReadDimensions(photo!.Thumbnail);
        Assert.Multiple(() =>
        {
            Assert.That(thumbnail.Width, Is.EqualTo(360));
            Assert.That(thumbnail.Height, Is.EqualTo(PhotoUploadLimits.MaxThumbnailEdge));
        });

        using var data = SKData.CreateCopy(photo.Original);
        using var bitmap = SKBitmap.Decode(data);
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.GetPixel(450, 200).Red, Is.GreaterThan(200), "Top-left quadrant should become top-right.");
            Assert.That(bitmap.GetPixel(150, 600).Red, Is.LessThan(160), "Bottom-left quadrant should stay background.");
            Assert.That(bitmap.GetPixel(0, 0).Red, Is.LessThan(160));
        });
    }

    [Test]
    public void TryProcess_CorruptJpeg_ReturnsFalse()
    {
        var content = TestImageFactory.CreateCorruptJpeg();

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(photo, Is.Null);
        });
    }

    [Test]
    public void TryProcess_UnsupportedSignature_ReturnsFalse()
    {
        var content = TestImageFactory.CreateText();

        var succeeded = _processor.TryProcess(content, out var photo);

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(photo, Is.Null);
        });
    }

    private static bool IsJpeg(byte[] content)
        => content.Length > 2 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

    private static (int Width, int Height) ReadDimensions(byte[] content)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);
        Assert.That(codec, Is.Not.Null);
        return (codec!.Info.Width, codec.Info.Height);
    }
}
