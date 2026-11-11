using System.Text;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class ImageHeaderReaderTests
{
    [Test]
    public void TryReadSize_WithPngHeader_ReturnsWidthAndHeight()
    {
        using var stream = new MemoryStream(BuildPngHeader(800, 600));

        var size = ImageHeaderReader.TryReadSize(stream);

        Assert.That(size, Is.EqualTo((800, 600)));
    }

    [Test]
    public void TryReadSize_WithPngZeroWidth_ReturnsNull()
    {
        using var stream = new MemoryStream(BuildPngHeader(0, 600));

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.Null);
    }

    [Test]
    public void TryReadSize_WithBaselineJpegAfterApp0_ReturnsWidthAndHeight()
    {
        using var stream = new MemoryStream(BuildJpegHeader(0xC0, 800, 600));

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.EqualTo((800, 600)));
    }

    [Test]
    public void TryReadSize_WithProgressiveJpeg_ReturnsWidthAndHeight()
    {
        using var stream = new MemoryStream(BuildJpegHeader(0xC2, 1024, 512));

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.EqualTo((1024, 512)));
    }

    [Test]
    public void TryReadSize_WithRepeatedSoiMarker_ReturnsWidthAndHeight()
    {
        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xD8 }; // SOI + repeated SOI
        bytes.AddRange(BuildSofSegment(0xC0, 800, 600));
        using var stream = new MemoryStream(bytes.ToArray());

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.EqualTo((800, 600)));
    }

    [Test]
    public void TryReadSize_WithStraySoiBetweenSegments_ReturnsWidthAndHeight()
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange(BuildApp0Segment());
        bytes.AddRange(new byte[] { 0xFF, 0xD8 }); // SOI has no length; must not consume 2 bytes
        bytes.AddRange(BuildSofSegment(0xC0, 640, 480));
        using var stream = new MemoryStream(bytes.ToArray());

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.EqualTo((640, 480)));
    }

    [Test]
    public void TryReadSize_WithGarbageBytes_ReturnsNull()
    {
        using var stream = new MemoryStream(new byte[16]);

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.Null);
    }

    [Test]
    public void TryReadSize_WithEmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.Null);
    }

    [Test]
    public void TryReadSize_WithNonSeekableStream_ReturnsNull()
    {
        using var inner = new MemoryStream(BuildPngHeader(800, 600));
        using var stream = new NonSeekableStream(inner);

        Assert.That(ImageHeaderReader.TryReadSize(stream), Is.Null);
    }

    [Test]
    public void TryReadSize_WithNullStream_ThrowsArgumentNullException()
    {
        Assert.That(() => ImageHeaderReader.TryReadSize(null!), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void TryReadSize_WhenStreamIsPartiallyRead_RestoresOriginalPosition()
    {
        using var stream = new MemoryStream(BuildPngHeader(800, 600));
        stream.Position = 5;

        ImageHeaderReader.TryReadSize(stream);

        Assert.That(stream.Position, Is.EqualTo(5));
    }

    private static byte[] BuildPngHeader(int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0D });
        bytes.AddRange(Encoding.ASCII.GetBytes("IHDR"));
        bytes.AddRange(ToBigEndian(width));
        bytes.AddRange(ToBigEndian(height));
        bytes.AddRange(new byte[] { 0x08, 0x06, 0x00, 0x00, 0x00 });
        return bytes.ToArray();
    }

    private static byte[] BuildJpegHeader(byte sofMarker, int width, int height)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 }; // SOI
        bytes.AddRange(BuildApp0Segment());
        bytes.AddRange(BuildSofSegment(sofMarker, width, height));
        return bytes.ToArray();
    }

    private static byte[] BuildApp0Segment()
        => new byte[]
        {
            0xFF, 0xE0, 0x00, 0x10, // APP0, length 16
            0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00 // JFIF payload
        };

    private static byte[] BuildSofSegment(byte sofMarker, int width, int height)
    {
        var bytes = new List<byte> { 0xFF, sofMarker, 0x00, 0x11, 0x08 }; // SOF, length 17, precision 8
        bytes.AddRange(ToBigEndian16(height));
        bytes.AddRange(ToBigEndian16(width));
        bytes.AddRange(new byte[] { 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
        return bytes.ToArray();
    }

    private static byte[] ToBigEndian(int value)
        => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    private static byte[] ToBigEndian16(int value)
        => new[] { (byte)(value >> 8), (byte)value };

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
