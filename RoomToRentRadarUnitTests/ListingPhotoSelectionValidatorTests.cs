using RoomToRentRadar.Features.ListingManagement.Models;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingPhotoSelectionValidatorTests
{
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegHeader = [255, 216, 255, 224, 0, 16, 74, 70];

    [TestCase("room.jpg", "image/jpeg")]
    [TestCase("room.JPEG", "image/jpeg")]
    [TestCase("room.PNG", "image/png")]
    public async Task CopyValidatedAsync_SupportedFile_CopiesBytesAndDeterminesContentType(string fileName, string contentType)
    {
        var bytes = contentType == "image/png" ? PngHeader : JpegHeader;
        using var source = new MemoryStream(bytes);
        using var destination = new MemoryStream();

        var result = await ListingPhotoSelectionValidator.CopyValidatedAsync(fileName, source, destination);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(contentType));
            Assert.That(destination.ToArray(), Is.EqualTo(bytes));
        });
    }

    [TestCase("room.webp")]
    [TestCase("room.gif")]
    [TestCase("room.txt")]
    [TestCase("room")]
    public void CopyValidatedAsync_UnsupportedExtension_RejectsEvenWithPngBytes(string fileName)
    {
        using var source = new MemoryStream(PngHeader);
        using var destination = new MemoryStream();

        Assert.ThrowsAsync<InvalidDataException>(() =>
            ListingPhotoSelectionValidator.CopyValidatedAsync(fileName, source, destination));
    }

    [Test]
    public void CopyValidatedAsync_EmptyFile_IsRejected()
    {
        using var source = new MemoryStream();
        using var destination = new MemoryStream();

        Assert.ThrowsAsync<InvalidDataException>(() =>
            ListingPhotoSelectionValidator.CopyValidatedAsync("room.jpg", source, destination));
    }

    [Test]
    public void CopyValidatedAsync_MismatchedExtensionAndContent_IsRejected()
    {
        using var source = new MemoryStream(PngHeader);
        using var destination = new MemoryStream();

        Assert.ThrowsAsync<InvalidDataException>(() =>
            ListingPhotoSelectionValidator.CopyValidatedAsync("room.jpg", source, destination));
    }

    [Test]
    public void CopyValidatedAsync_PretendImageWithTextBytes_IsRejected()
    {
        using var source = new MemoryStream("not an image"u8.ToArray());
        using var destination = new MemoryStream();

        Assert.ThrowsAsync<InvalidDataException>(() =>
            ListingPhotoSelectionValidator.CopyValidatedAsync("room.png", source, destination));
    }

    [Test]
    public async Task CopyValidatedAsync_ExactlyTenMegabytes_IsAccepted()
    {
        var bytes = new byte[(int)ListingPhotoSelectionValidator.MaxFileBytes];
        PngHeader.CopyTo(bytes, 0);
        using var source = new MemoryStream(bytes);
        using var destination = new MemoryStream();

        var result = await ListingPhotoSelectionValidator.CopyValidatedAsync("room.png", source, destination);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("image/png"));
            Assert.That(destination.Length, Is.EqualTo(ListingPhotoSelectionValidator.MaxFileBytes));
        });
    }

    [Test]
    public void CopyValidatedAsync_OneByteOverTenMegabytes_IsRejected()
    {
        var bytes = new byte[(int)ListingPhotoSelectionValidator.MaxFileBytes + 1];
        PngHeader.CopyTo(bytes, 0);
        using var source = new MemoryStream(bytes);
        using var destination = new MemoryStream();

        Assert.ThrowsAsync<InvalidDataException>(() =>
            ListingPhotoSelectionValidator.CopyValidatedAsync("room.png", source, destination));
        Assert.That(destination.Length, Is.LessThanOrEqualTo(ListingPhotoSelectionValidator.MaxFileBytes));
    }

    [Test]
    public async Task CopyValidatedAsync_NonSeekableStreamWithSplitHeader_ValidatesWithoutLengthOrSeeking()
    {
        using var source = new ChunkedStream(PngHeader);
        using var destination = new MemoryStream();

        var result = await ListingPhotoSelectionValidator.CopyValidatedAsync("room.png", source, destination);

        Assert.That(result, Is.EqualTo("image/png"));
        Assert.That(destination.ToArray(), Is.EqualTo(PngHeader));
    }

    private sealed class ChunkedStream(byte[] bytes) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position == bytes.Length || count == 0) return 0;
            buffer[offset] = bytes[_position++];
            return 1;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_position == bytes.Length || buffer.Length == 0) return ValueTask.FromResult(0);
            buffer.Span[0] = bytes[_position++];
            return ValueTask.FromResult(1);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
