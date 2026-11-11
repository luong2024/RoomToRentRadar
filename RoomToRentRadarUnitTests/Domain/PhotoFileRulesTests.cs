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

    [TestCase("room-123")]
    [TestCase("abc_DEF-09")]
    public void IsValidRoomId_WithValidId_ReturnsTrue(string roomId)
    {
        Assert.That(PhotoFileRules.IsValidRoomId(roomId), Is.True);
    }

    [Test]
    public void IsValidRoomId_With64Characters_ReturnsTrue()
    {
        Assert.That(PhotoFileRules.IsValidRoomId(new string('a', 64)), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("../etc/passwd")]
    [TestCase("a/b")]
    [TestCase("room.1")]
    [TestCase("room-1\n")]
    public void IsValidRoomId_WithInvalidId_ReturnsFalse(string? roomId)
    {
        Assert.That(PhotoFileRules.IsValidRoomId(roomId), Is.False);
    }

    [Test]
    public void IsValidRoomId_With65Characters_ReturnsFalse()
    {
        Assert.That(PhotoFileRules.IsValidRoomId(new string('a', 65)), Is.False);
    }
}
