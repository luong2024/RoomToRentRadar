using RoomToRentRadar.Infrastructure.Adapters.Storage;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class LocalDiskPhotoStorageAdapterTests
{
    private const string BaseUrl = "http://localhost:5000";

    private string _rootPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "rtrr-photos", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a locked temp file must not fail the test run.
        }
    }

    [Test]
    public async Task SaveAsync_WritesFileAndCreatesDirectories()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);
        var expectedBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        Assert.That(Directory.Exists(_rootPath), Is.False);

        using var content = new MemoryStream(expectedBytes);
        await adapter.SaveAsync("rooms/2026/11/p1/original.jpg", content);

        var expectedPath = Path.Combine(_rootPath, "rooms", "2026", "11", "p1", "original.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(expectedPath), Is.True);
            Assert.That(File.ReadAllBytes(expectedPath), Is.EqualTo(expectedBytes));
        });
    }

    [Test]
    public async Task SaveAsync_WithTraversalSegments_ThrowsAndWritesNothing()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);
        const string legitPath = "rooms/2026/11/p1/original.jpg";
        var legitBytes = new byte[] { 7, 7, 7 };

        using (var legitContent = new MemoryStream(legitBytes))
        {
            await adapter.SaveAsync(legitPath, legitContent);
        }

        // Both traversal keys resolve to "<tmp>/rtrr-photos/evil-<id>.jpg", one level above the root.
        var escapedName = $"evil-{Path.GetFileName(_rootPath)}.jpg";
        var escapedPath = Path.Combine(Path.GetDirectoryName(_rootPath)!, escapedName);

        using var dotDotContent = new MemoryStream(new byte[] { 1 });
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await adapter.SaveAsync($"../{escapedName}", dotDotContent));

        using var nestedContent = new MemoryStream(new byte[] { 2 });
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await adapter.SaveAsync($"rooms/../../{escapedName}", nestedContent));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(escapedPath), Is.False);
            Assert.That(
                File.ReadAllBytes(Path.Combine(_rootPath, "rooms", "2026", "11", "p1", "original.jpg")),
                Is.EqualTo(legitBytes));
        });
    }

    [Test]
    public async Task SaveAsync_WithRootedPath_Throws()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);
        var rootedPath = Path.Combine(Path.GetTempPath(), $"rtrr-rooted-{Guid.NewGuid():N}.jpg");

        using var content = new MemoryStream(new byte[] { 1 });
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await adapter.SaveAsync(rootedPath, content));

        Assert.That(File.Exists(rootedPath), Is.False);
    }

    [Test]
    public async Task SaveAsync_WithExistingFile_Throws()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);
        const string path = "rooms/2026/11/p1/original.jpg";
        var originalBytes = new byte[] { 9, 8, 7 };

        using (var firstContent = new MemoryStream(originalBytes))
        {
            await adapter.SaveAsync(path, firstContent);
        }

        using var secondContent = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.ThrowsAsync<IOException>(
            async () => await adapter.SaveAsync(path, secondContent));

        var storedBytes = await File.ReadAllBytesAsync(
            Path.Combine(_rootPath, "rooms", "2026", "11", "p1", "original.jpg"));
        Assert.That(storedBytes, Is.EqualTo(originalBytes));
    }

    [Test]
    public async Task SaveAsync_WithNonSeekableStream_WritesFile()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);
        var expectedBytes = new byte[] { 10, 20, 30, 40, 50 };
        using var content = new NonSeekableReadOnlyStream(expectedBytes);

        await adapter.SaveAsync("rooms/2026/11/p1/original.jpg", content);

        var storedBytes = await File.ReadAllBytesAsync(
            Path.Combine(_rootPath, "rooms", "2026", "11", "p1", "original.jpg"));
        Assert.That(storedBytes, Is.EqualTo(expectedBytes));
    }

    [Test]
    public async Task DeleteAsync_RemovesExistingFileAndDoesNotThrowWhenMissing()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);
        const string path = "rooms/2026/11/p1/original.jpg";

        using (var content = new MemoryStream(new byte[] { 4, 5, 6 }))
        {
            await adapter.SaveAsync(path, content);
        }

        var filePath = Path.Combine(_rootPath, "rooms", "2026", "11", "p1", "original.jpg");
        Assert.That(File.Exists(filePath), Is.True);

        await adapter.DeleteAsync(path);
        Assert.That(File.Exists(filePath), Is.False);

        Assert.DoesNotThrowAsync(async () => await adapter.DeleteAsync(path));
    }

    [Test]
    public void GetPublicUrl_ReturnsAbsoluteMediaUrl()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);

        var url = adapter.GetPublicUrl("rooms/2026/11/p1/original.jpg");

        Assert.That(url, Is.EqualTo("http://localhost:5000/media/rooms/2026/11/p1/original.jpg"));
    }

    [Test]
    public void GetPublicUrl_TrimsSlashes()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl + "/");

        var url = adapter.GetPublicUrl("/rooms/2026/11/p1/original.jpg");

        Assert.That(url, Is.EqualTo("http://localhost:5000/media/rooms/2026/11/p1/original.jpg"));
    }

    [Test]
    public void GetPublicUrl_NormalizesBackslashes()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath, BaseUrl);

        var url = adapter.GetPublicUrl(@"rooms\2026\x.jpg");

        Assert.That(url, Is.EqualTo("http://localhost:5000/media/rooms/2026/x.jpg"));
    }

    [Test]
    public void Constructor_WithEmptyRootPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LocalDiskPhotoStorageAdapter("   ", BaseUrl));
    }

    [Test]
    public void Constructor_WithEmptyPublicBaseUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LocalDiskPhotoStorageAdapter(_rootPath, "   "));
    }

    private sealed class NonSeekableReadOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableReadOnlyStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

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
