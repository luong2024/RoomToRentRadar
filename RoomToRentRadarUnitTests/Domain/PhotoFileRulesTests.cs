using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>BR-MED-01..03: magic-byte detection, size limits and panorama aspect ratio.</summary>
[TestFixture]
public class PhotoFileRulesTests
{
    [Test]
    public void Constants_MatchBusinessRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PhotoFileRules.MaxFileBytes, Is.EqualTo(10_485_760)); // BR-MED-02
            Assert.That(PhotoFileRules.UnattachedPhotoLifetime, Is.EqualTo(TimeSpan.FromHours(24))); // BR-MED-03
            Assert.That(PhotoFileRules.MinStandardPhotos, Is.EqualTo(3)); // BR-MED-01
            Assert.That(PhotoFileRules.MaxStandardPhotos, Is.EqualTo(20)); // BR-MED-01
            Assert.That(PhotoFileRules.MaxPanoramaPhotos, Is.EqualTo(3)); // BR-MED-01
        });
    }

    [Test]
    public void DetectExtension_JpegMagicBytes_ReturnsJpg()
    {
        ReadOnlySpan<byte> header = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        Assert.That(PhotoFileRules.DetectExtension(header), Is.EqualTo(".jpg"));
    }

    [Test]
    public void DetectExtension_PngMagicBytes_ReturnsPng()
    {
        ReadOnlySpan<byte> header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

        Assert.That(PhotoFileRules.DetectExtension(header), Is.EqualTo(".png"));
    }

    [Test]
    public void DetectExtension_GifBytes_ReturnsNull()
    {
        ReadOnlySpan<byte> header = "GIF89a"u8;

        Assert.That(PhotoFileRules.DetectExtension(header), Is.Null);
    }

    [Test]
    public void DetectExtension_TextContentNamedLikeJpeg_ReturnsNull()
    {
        ReadOnlySpan<byte> header = "this is not an image"u8;

        Assert.That(PhotoFileRules.DetectExtension(header), Is.Null, "Phải kiểm tra magic bytes, không dựa vào đuôi file.");
    }

    [Test]
    public void DetectExtension_TruncatedJpegMagic_ReturnsNull()
    {
        ReadOnlySpan<byte> header = [0xFF, 0xD8];

        Assert.That(PhotoFileRules.DetectExtension(header), Is.Null);
    }

    [Test]
    public void DetectExtension_EmptyHeader_ReturnsNull()
    {
        Assert.That(PhotoFileRules.DetectExtension([]), Is.Null);
    }

    [TestCase(4000, 2000, true, TestName = "IsPanoramaRatio_Exact2To1_IsTrue")]
    [TestCase(2000, 1000, true, TestName = "IsPanoramaRatio_SmallImage2To1_IsTrue")]
    [TestCase(2040, 1000, true, TestName = "IsPanoramaRatio_UpperBoundary_IsTrue")]
    [TestCase(1960, 1000, true, TestName = "IsPanoramaRatio_LowerBoundary_IsTrue")]
    [TestCase(2041, 1000, false, TestName = "IsPanoramaRatio_AboveUpperBoundary_IsFalse")]
    [TestCase(1959, 1000, false, TestName = "IsPanoramaRatio_BelowLowerBoundary_IsFalse")]
    [TestCase(4000, 3000, false, TestName = "IsPanoramaRatio_4To3_IsFalse")]
    [TestCase(0, 1000, false, TestName = "IsPanoramaRatio_ZeroWidth_IsFalse")]
    [TestCase(2000, 0, false, TestName = "IsPanoramaRatio_ZeroHeight_IsFalse")]
    [TestCase(-4000, 2000, false, TestName = "IsPanoramaRatio_NegativeWidth_IsFalse")]
    public void IsPanoramaRatio_BoundariesAndInvalidValues_ReturnExpected(int width, int height, bool expected)
    {
        Assert.That(PhotoFileRules.IsPanoramaRatio(width, height), Is.EqualTo(expected));
    }
}
