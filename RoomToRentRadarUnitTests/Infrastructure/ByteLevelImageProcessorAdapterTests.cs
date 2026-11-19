using System.Text;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Infrastructure.Adapters.Media;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// BR-MED-04 / ADR-008: EXIF (incl. GPS) is stripped at byte level without re-encoding; malformed
/// input is passed through unchanged for the upload use case to reject.
/// </summary>
[TestFixture]
public class ByteLevelImageProcessorAdapterTests
{
    private const int ExifApp1SegmentLength = 14; // marker + length + "Exif\0\0" + 4 payload bytes
    private const int PngExifChunkLength = 12 + 7; // length + type + 7 data bytes + CRC

    private readonly ByteLevelImageProcessorAdapter _adapter = new();

    [Test]
    public async Task ProcessAndStripExifAsync_JpegWithExifApp1_RemovesApp1AndKeepsImageData()
    {
        var original = BuildJpeg(width: 4000, height: 2000, includeExif: true);
        var expected = BuildJpeg(width: 4000, height: 2000, includeExif: false);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".jpg");
        var position = result.Position;
        var cleaned = ReadAll(result);

        Assert.Multiple(() =>
        {
            Assert.That(position, Is.Zero, "The cleaned stream must be positioned at 0.");
            Assert.That(Contains(original, Encoding.ASCII.GetBytes("Exif")), Is.True);
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("Exif")), Is.False, "EXIF payload must be removed.");
            Assert.That(cleaned, Is.EqualTo(expected), "Only the APP1 segment may be dropped; every other byte is preserved.");
            Assert.That(cleaned, Has.Length.EqualTo(original.Length - ExifApp1SegmentLength));
            Assert.That(cleaned[0], Is.EqualTo(0xFF));
            Assert.That(cleaned[1], Is.EqualTo(0xD8));
            Assert.That(cleaned[^2], Is.EqualTo(0xFF));
            Assert.That(cleaned[^1], Is.EqualTo(0xD9));
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("JFIF")), Is.True, "APP0 must be preserved.");
            Assert.That(Contains(cleaned, new byte[] { 0x11, 0x22, 0x33, 0x44 }), Is.True, "Scan data must be preserved.");
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_JpegWithExifAndXmpApp1_RemovesBothApp1Segments()
    {
        var original = BuildJpeg(width: 1920, height: 1080, includeExif: true, includeXmp: true);
        var expected = BuildJpeg(width: 1920, height: 1080, includeExif: false);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".jpeg");
        var cleaned = ReadAll(result);

        Assert.Multiple(() =>
        {
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("Exif")), Is.False);
            Assert.That(
                Contains(cleaned, Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/")),
                Is.False,
                "The XMP APP1 segment must be removed as well.");
            Assert.That(cleaned, Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_JpegWithFillerBytes_StripsApp1AndPreservesFiller()
    {
        // JPEG allows 0xFF fill bytes before a marker; stripping must not lose or duplicate them.
        var original = BuildJpeg(width: 800, height: 600, includeExif: true, fillBytesBeforeApp0: 2);
        var expected = BuildJpeg(width: 800, height: 600, includeExif: false, fillBytesBeforeApp0: 2);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".jpg");
        var cleaned = ReadAll(result);

        Assert.Multiple(() =>
        {
            Assert.That(cleaned, Is.EqualTo(expected));
            Assert.That(cleaned, Has.Length.EqualTo(original.Length - ExifApp1SegmentLength));
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_CleanedJpeg_StillHasReadableDimensions()
    {
        var original = BuildJpeg(width: 4000, height: 2000, includeExif: true);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".jpg");

        var parsed = ImageHeaderReader.TryReadSize(result, out var width, out var height);
        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(width, Is.EqualTo(4000));
            Assert.That(height, Is.EqualTo(2000));
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_JpegWithoutExif_ReturnsUnchangedCopy()
    {
        var original = BuildJpeg(width: 1280, height: 720, includeExif: false);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".jpg");

        Assert.That(ReadAll(result), Is.EqualTo(original));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_PngWithExifChunk_RemovesOnlyExifChunk()
    {
        var original = BuildPng(width: 2048, height: 1024, includeExif: true);
        var expected = BuildPng(width: 2048, height: 1024, includeExif: false);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".png");
        var position = result.Position;
        var cleaned = ReadAll(result);

        Assert.Multiple(() =>
        {
            Assert.That(position, Is.Zero);
            Assert.That(Contains(original, Encoding.ASCII.GetBytes("eXIf")), Is.True);
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("eXIf")), Is.False);
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("IHDR")), Is.True);
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("IDAT")), Is.True);
            Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("IEND")), Is.True);
            Assert.That(cleaned, Has.Length.EqualTo(original.Length - PngExifChunkLength));
            Assert.That(cleaned, Is.EqualTo(expected), "All other chunks (including their CRC) must be preserved byte for byte.");
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_CleanedPng_StillHasReadableDimensions()
    {
        var original = BuildPng(width: 2048, height: 1024, includeExif: true);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".png");

        var parsed = ImageHeaderReader.TryReadSize(result, out var width, out var height);
        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(width, Is.EqualTo(2048));
            Assert.That(height, Is.EqualTo(1024));
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_PngWithoutExif_ReturnsUnchangedCopy()
    {
        var original = BuildPng(width: 640, height: 480, includeExif: false);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), ".png");

        Assert.That(ReadAll(result), Is.EqualTo(original));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_MalformedJpeg_ReturnsUnchangedCopy()
    {
        var malformed = new byte[] { 0xFF, 0xD8, 0x00, 0x11, 0x22 };

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(malformed), ".jpg");

        Assert.That(ReadAll(result), Is.EqualTo(malformed));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_TruncatedJpegSegment_ReturnsUnchangedCopy()
    {
        // SOI + APP1 marker whose declared length runs past the end of the stream.
        var truncated = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x40, 0x45, 0x78 };

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(truncated), ".jpg");

        Assert.That(ReadAll(result), Is.EqualTo(truncated));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_MalformedPng_ReturnsUnchangedCopy()
    {
        var malformed = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(malformed), ".png");

        Assert.That(ReadAll(result), Is.EqualTo(malformed));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_PngChunkLengthPastEnd_ReturnsUnchangedCopy()
    {
        // PNG signature + an "eXIf" chunk header that claims 16 data bytes but the stream ends early.
        var truncated = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x10,
            (byte)'e', (byte)'X', (byte)'I', (byte)'f',
            0x01, 0x02, 0x03, 0x04
        };

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(truncated), ".png");

        Assert.That(ReadAll(result), Is.EqualTo(truncated));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_TextContent_ReturnsUnchangedCopy()
    {
        var text = "this is not an image"u8.ToArray();

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(text), ".jpg");

        Assert.That(ReadAll(result), Is.EqualTo(text));
    }

    [Test]
    public async Task ProcessAndStripExifAsync_EmptyInput_ReturnsEmptyStream()
    {
        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(), ".png");

        Assert.Multiple(() =>
        {
            Assert.That(result.Length, Is.Zero);
            Assert.That(result.Position, Is.Zero);
        });
    }

    [Test]
    public async Task ProcessAndStripExifAsync_UnknownExtension_DetectsFormatFromMagicBytes()
    {
        var original = BuildPng(width: 800, height: 400, includeExif: true);

        using var result = await _adapter.ProcessAndStripExifAsync(new MemoryStream(original), string.Empty);
        var cleaned = ReadAll(result);

        Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("eXIf")), Is.False);
    }

    [Test]
    public async Task ProcessAndStripExifAsync_NonSeekableInput_IsBuffered()
    {
        var original = BuildJpeg(width: 4000, height: 2000, includeExif: true);

        using var result = await _adapter.ProcessAndStripExifAsync(new NonSeekableStream(original), ".jpg");
        var cleaned = ReadAll(result);

        Assert.That(Contains(cleaned, Encoding.ASCII.GetBytes("Exif")), Is.False);
    }

    [Test]
    public async Task ProcessAndStripExifAsync_ReturnsReadOnlyStreamAndLeavesInputOpen()
    {
        var original = BuildJpeg(width: 640, height: 480, includeExif: true);
        using var input = new MemoryStream(original);

        using var result = await _adapter.ProcessAndStripExifAsync(input, ".jpg");

        Assert.Multiple(() =>
        {
            Assert.That(result.Position, Is.Zero);
            Assert.That(result.CanSeek, Is.True);
            Assert.That(result.CanWrite, Is.False, "The returned stream must be read-only.");
            Assert.That(input.CanRead, Is.True, "The adapter must not dispose the caller's stream.");
            Assert.That(input.Position, Is.EqualTo(original.Length));
        });
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    /// <summary>SOI + APP0 + optional APP1/Exif + optional APP1/XMP + SOF0 + SOS + scan data + EOI.</summary>
    private static byte[] BuildJpeg(
        int width,
        int height,
        bool includeExif,
        bool includeXmp = false,
        int fillBytesBeforeApp0 = 0)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        // Optional 0xFF fill bytes before the first length-bearing marker.
        for (var i = 0; i < fillBytesBeforeApp0; i++)
        {
            bytes.Add(0xFF);
        }

        // APP0/JFIF: marker + length(16) + "JFIF\0" + version/units/density.
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        bytes.AddRange([0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        if (includeExif)
        {
            // APP1: marker + length(12) + "Exif\0\0" + 4 GPS bytes.
            bytes.AddRange([0xFF, 0xE1, 0x00, 0x0C]);
            bytes.AddRange("Exif\u0000\u0000"u8.ToArray());
            bytes.AddRange([0x01, 0x02, 0x03, 0x04]);
        }

        if (includeXmp)
        {
            var xmp = new List<byte>();
            xmp.AddRange(Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/"));
            xmp.Add(0x00);
            xmp.AddRange("GPSDATA"u8.ToArray());
            var length = xmp.Count + 2;
            bytes.AddRange([0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF)]);
            bytes.AddRange(xmp);
        }

        // SOF0: precision + height + width + 3 components.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.Add((byte)(height >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(width & 0xFF));
        bytes.Add(0x03);
        bytes.AddRange([0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);

        // SOS: length 8 + 6 payload bytes, then entropy-coded data and EOI.
        bytes.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        bytes.AddRange([0x11, 0x22, 0x33, 0x44]);
        bytes.AddRange([0xFF, 0xD9]);

        return bytes.ToArray();
    }

    /// <summary>PNG signature + IHDR + optional eXIf + IDAT + IEND with real CRC-32 values.</summary>
    private static byte[] BuildPng(int width, int height, bool includeExif)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var ihdr = new List<byte>();
        ihdr.AddRange([(byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width]);
        ihdr.AddRange([(byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height]);
        ihdr.AddRange([0x08, 0x02, 0x00, 0x00, 0x00]);
        AddChunk(bytes, "IHDR", ihdr.ToArray());

        if (includeExif)
        {
            AddChunk(bytes, "eXIf", "GPSDATA"u8.ToArray());
        }

        AddChunk(bytes, "IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]);
        AddChunk(bytes, "IEND", []);

        return bytes.ToArray();
    }

    private static void AddChunk(List<byte> bytes, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = data.Length;
        bytes.AddRange([(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length]);
        bytes.AddRange(typeBytes);
        bytes.AddRange(data);

        var crc = Crc32([.. typeBytes, .. data]);
        bytes.AddRange([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
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
