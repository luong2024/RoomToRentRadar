using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>
/// BR-MED-02 / ADR-008: JPEG (SOF0/SOF1/SOF2) and PNG (IHDR) header parsing with real binary
/// headers, including malformed input that must never throw.
/// </summary>
[TestFixture]
public class ImageHeaderReaderTests
{
    [Test]
    public void TryReadSize_JpegSof0_ReturnsDimensions()
    {
        using var stream = new MemoryStream(BuildJpeg(4000, 3000, sofMarker: 0xC0));

        var result = ImageHeaderReader.TryReadSize(stream, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(width, Is.EqualTo(4000));
            Assert.That(height, Is.EqualTo(3000));
        });
    }

    [Test]
    public void TryReadSize_JpegSof1_ReturnsDimensions()
    {
        using var stream = new MemoryStream(BuildJpeg(1280, 720, sofMarker: 0xC1));

        var result = ImageHeaderReader.TryReadSize(stream, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(width, Is.EqualTo(1280));
            Assert.That(height, Is.EqualTo(720));
        });
    }

    [Test]
    public void TryReadSize_ProgressiveJpegSof2_ReturnsDimensions()
    {
        using var stream = new MemoryStream(BuildJpeg(8000, 4000, sofMarker: 0xC2));

        var result = ImageHeaderReader.TryReadSize(stream, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(width, Is.EqualTo(8000)); // panorama 2:1
            Assert.That(height, Is.EqualTo(4000));
        });
    }

    [Test]
    public void TryReadSize_JpegWithApp1SegmentBeforeSof_SkipsSegmentAndReturnsDimensions()
    {
        using var stream = new MemoryStream(BuildJpeg(4000, 2000, sofMarker: 0xC0, app1PayloadBytes: 300));

        var result = ImageHeaderReader.TryReadSize(stream, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(width, Is.EqualTo(4000));
            Assert.That(height, Is.EqualTo(2000));
        });
    }

    [Test]
    public void TryReadSize_PngIhdr_ReturnsDimensions()
    {
        using var stream = new MemoryStream(BuildPng(4000, 2000));

        var result = ImageHeaderReader.TryReadSize(stream, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(width, Is.EqualTo(4000));
            Assert.That(height, Is.EqualTo(2000));
        });
    }

    [Test]
    public void TryReadSize_NullStream_ReturnsFalse()
    {
        var result = ImageHeaderReader.TryReadSize(null!, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(width, Is.Zero);
            Assert.That(height, Is.Zero);
        });
    }

    [Test]
    public void TryReadSize_EmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();

        Assert.That(ImageHeaderReader.TryReadSize(stream, out _, out _), Is.False);
    }

    [Test]
    public void TryReadSize_TextContentNamedLikeJpeg_ReturnsFalse()
    {
        using var stream = new MemoryStream("JFIF but actually text"u8.ToArray());

        Assert.That(ImageHeaderReader.TryReadSize(stream, out _, out _), Is.False);
    }

    [Test]
    public void TryReadSize_GifHeader_ReturnsFalse()
    {
        using var stream = new MemoryStream("GIF89a\u0001\u0000\u0001\u0000"u8.ToArray());

        Assert.That(ImageHeaderReader.TryReadSize(stream, out _, out _), Is.False);
    }

    [Test]
    public void TryReadSize_TruncatedJpegWithoutSof_ReturnsFalse()
    {
        using var stream = new MemoryStream([0xFF, 0xD8]);

        Assert.That(ImageHeaderReader.TryReadSize(stream, out _, out _), Is.False);
    }

    [Test]
    public void TryReadSize_JpegWithZeroWidth_ReturnsFalse()
    {
        using var stream = new MemoryStream(BuildJpeg(0, 2000));

        Assert.That(ImageHeaderReader.TryReadSize(stream, out _, out _), Is.False);
    }

    [Test]
    public void TryReadSize_PngSignatureOnly_ReturnsFalse()
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Assert.That(ImageHeaderReader.TryReadSize(stream, out _, out _), Is.False);
    }

    [Test]
    public void TryReadSize_SeekableStream_RestoresOriginalPosition()
    {
        using var stream = new MemoryStream(BuildJpeg(4000, 2000));
        stream.Position = 5;

        var result = ImageHeaderReader.TryReadSize(stream, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(stream.Position, Is.EqualTo(5));
        });
    }

    [Test]
    public void TryReadSize_NonSeekableStream_BuffersAndReturnsDimensions()
    {
        using var stream = new NonSeekableStream(BuildPng(2048, 1024));

        var result = ImageHeaderReader.TryReadSize(stream, out var width, out var height);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(width, Is.EqualTo(2048));
            Assert.That(height, Is.EqualTo(1024));
        });
    }

    /// <summary>
    /// Builds a structurally valid JPEG header: SOI, optional APP0/APP1 segments, one SOF segment and EOI.
    /// </summary>
    private static byte[] BuildJpeg(int width, int height, byte sofMarker = 0xC0, int app1PayloadBytes = 0)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 }; // SOI

        // APP0 (JFIF): length 16 includes the two length bytes, payload 14 bytes.
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        bytes.AddRange([0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        if (app1PayloadBytes > 0)
        {
            var segmentLength = app1PayloadBytes + 2;
            bytes.AddRange([0xFF, 0xE1, (byte)(segmentLength >> 8), (byte)(segmentLength & 0xFF)]);
            bytes.AddRange(Enumerable.Repeat((byte)0xAA, app1PayloadBytes));
        }

        // SOF: precision(1) + height(2) + width(2) + components(1) + 3 × 3 bytes = 15 payload bytes.
        bytes.AddRange([0xFF, sofMarker, 0x00, 0x11, 0x08]);
        bytes.Add((byte)(height >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(width & 0xFF));
        bytes.Add(0x03);
        bytes.AddRange([0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);

        bytes.AddRange([0xFF, 0xD9]); // EOI
        return bytes.ToArray();
    }

    /// <summary>Builds a structurally valid PNG header: signature + IHDR chunk.</summary>
    private static byte[] BuildPng(int width, int height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        bytes.AddRange([0x00, 0x00, 0x00, 0x0D]); // IHDR data length = 13
        bytes.AddRange([0x49, 0x48, 0x44, 0x52]); // "IHDR"
        bytes.AddRange([(byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width]);
        bytes.AddRange([(byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height]);
        bytes.AddRange([0x08, 0x02, 0x00, 0x00, 0x00]); // bit depth, truecolour, deflate, filter, no interlace
        bytes.AddRange([0x00, 0x00, 0x00, 0x00]); // CRC placeholder
        return bytes.ToArray();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
