using RoomToRentRadar.Infrastructure.Adapters.Storage;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class LocalDiskPhotoStorageAdapterTests
{
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
    public async Task SaveAsync_WritesFileAndReturnsRelativeUrl()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath);
        var expectedBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        using var content = new MemoryStream(expectedBytes);
        var url = await adapter.SaveAsync("room-1", "photo.jpg", content);

        var expectedPath = Path.Combine(_rootPath, "rooms", "room-1", "photo.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(url, Is.EqualTo("/uploads/rooms/room-1/photo.jpg"));
            Assert.That(File.Exists(expectedPath), Is.True);
            Assert.That(File.ReadAllBytes(expectedPath), Is.EqualTo(expectedBytes));
        });
    }

    [Test]
    public async Task SaveAsync_CreatesRoomDirectoryWhenItDoesNotExist()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath);
        var roomDirectory = Path.Combine(_rootPath, "rooms", "room-2");

        Assert.That(Directory.Exists(roomDirectory), Is.False);

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        await adapter.SaveAsync("room-2", "first.png", content);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(roomDirectory), Is.True);
            Assert.That(File.Exists(Path.Combine(roomDirectory, "first.png")), Is.True);
        });
    }

    [Test]
    public void SaveAsync_WithTraversalSegments_ThrowsAndDoesNotWriteAnyFile()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath);
        var escapedFileNamePath = Path.Combine(_rootPath, "rooms", "evil.jpg");
        var escapedRoomIdPath = Path.Combine(_rootPath, "escape", "photo.jpg");

        using var fileNameContent = new MemoryStream(new byte[] { 1 });
        Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.SaveAsync("room-3", "../evil.jpg", fileNameContent));

        using var roomIdContent = new MemoryStream(new byte[] { 1 });
        Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.SaveAsync("../escape", "photo.jpg", roomIdContent));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(escapedFileNamePath), Is.False);
            Assert.That(File.Exists(escapedRoomIdPath), Is.False);
            Assert.That(Directory.Exists(_rootPath), Is.False);
        });
    }

    [Test]
    public async Task DeleteAsync_RemovesExistingFileAndDoesNotThrowWhenMissing()
    {
        var adapter = new LocalDiskPhotoStorageAdapter(_rootPath);
        using var content = new MemoryStream(new byte[] { 4, 5, 6 });
        await adapter.SaveAsync("room-4", "delete-me.jpg", content);

        var filePath = Path.Combine(_rootPath, "rooms", "room-4", "delete-me.jpg");
        Assert.That(File.Exists(filePath), Is.True);

        await adapter.DeleteAsync("room-4", "delete-me.jpg");
        Assert.That(File.Exists(filePath), Is.False);

        Assert.DoesNotThrowAsync(() => adapter.DeleteAsync("room-4", "delete-me.jpg"));
    }
}
