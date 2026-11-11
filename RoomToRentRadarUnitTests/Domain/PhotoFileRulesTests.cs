using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class PhotoFileRulesTests
{
    private static readonly byte[] JpgHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] GifHeader = { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00 };

    [Test]
    public void DetectExtension_WithJpgMagicBytes_ReturnsJpg()
    {
        Assert.That(PhotoFileRules.DetectExtension(JpgHeader), Is.EqualTo("jpg"));
    }

    [Test]
    public void DetectExtension_WithPngMagicBytes_ReturnsPng()
    {
        Assert.That(PhotoFileRules.DetectExtension(PngHeader), Is.EqualTo("png"));
    }

    [Test]
    public void DetectExtension_WithGifMagicBytes_ReturnsNull()
    {
        Assert.That(PhotoFileRules.DetectExtension(GifHeader), Is.Null);
    }

    [Test]
    public void DetectExtension_WithEmptyHeader_ReturnsNull()
    {
        Assert.That(PhotoFileRules.DetectExtension(ReadOnlySpan<byte>.Empty), Is.Null);
    }

    [Test]
    public void DetectExtension_WithTwoByteHeader_ReturnsNull()
    {
        Assert.That(PhotoFileRules.DetectExtension(new byte[] { 0xFF, 0xD8 }), Is.Null);
    }

    [Test]
    public void IsPanoramaRatio_WithExactTwoToOne_ReturnsTrue()
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(4000, 2000), Is.True);
    }

    [Test]
    public void IsPanoramaRatio_WithLowerToleranceBoundary_ReturnsTrue()
    {
        // 3920 / 2000 = 1.96 (2:1 minus 2%) — BR-MED panoramic ratio.
        Assert.That(PhotoFileRules.IsPanoramaRatio(3920, 2000), Is.True);
    }

    [Test]
    public void IsPanoramaRatio_WithUpperToleranceBoundary_ReturnsTrue()
    {
        // 4080 / 2000 = 2.04 (2:1 plus 2%).
        Assert.That(PhotoFileRules.IsPanoramaRatio(4080, 2000), Is.True);
    }

    [Test]
    public void IsPanoramaRatio_JustBelowLowerBoundary_ReturnsFalse()
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(3900, 2000), Is.False);
    }

    [Test]
    public void IsPanoramaRatio_JustAboveUpperBoundary_ReturnsFalse()
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(4100, 2000), Is.False);
    }

    [Test]
    public void IsPanoramaRatio_WithSquareImage_ReturnsFalse()
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(2000, 2000), Is.False);
    }

    [Test]
    public void IsPanoramaRatio_WithZeroWidth_ReturnsFalse()
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(0, 2000), Is.False);
    }

    [Test]
    public void IsPanoramaRatio_WithZeroHeight_ReturnsFalse()
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(4000, 0), Is.False);
    }

    [Test]
    public void ContentTypeOf_WithJpgExtension_ReturnsJpegContentType()
    {
        Assert.That(PhotoFileRules.ContentTypeOf("jpg"), Is.EqualTo("image/jpeg"));
    }

    [Test]
    public void ContentTypeOf_WithPngExtension_ReturnsPngContentType()
    {
        Assert.That(PhotoFileRules.ContentTypeOf("png"), Is.EqualTo("image/png"));
    }

    [Test]
    public void ContentTypeOf_WithUnsupportedExtension_ReturnsNull()
    {
        Assert.That(PhotoFileRules.ContentTypeOf("gif"), Is.Null);
    }

    [Test]
    public void ContentTypeOf_WithNullExtension_ReturnsNull()
    {
        Assert.That(PhotoFileRules.ContentTypeOf(null), Is.Null);
    }

    [Test]
    public void ContentTypeOf_WithEmptyExtension_ReturnsNull()
    {
        Assert.That(PhotoFileRules.ContentTypeOf(string.Empty), Is.Null);
    }
}
