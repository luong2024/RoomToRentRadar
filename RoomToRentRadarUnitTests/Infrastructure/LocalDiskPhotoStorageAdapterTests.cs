using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Media;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>ADR-008: local-disk storage behind <c>IPhotoStoragePort</c>, path-traversal safe.</summary>
[TestFixture]
public class LocalDiskPhotoStorageAdapterTests
{
    private string _rootPath = null!;

    [SetUp]
    public void SetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "rtrr-media-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private LocalDiskPhotoStorageAdapter CreateAdapter(
        string publicBaseUrl = "http://localhost:5000",
        string? rootPath = null) =>
        new(new MediaSettings { RootPath = rootPath ?? _rootPath, PublicBaseUrl = publicBaseUrl });

    [Test]
    public async Task SaveAsync_ValidKey_WritesContentUnderRootAndCreatesDirectories()
    {
        var adapter = CreateAdapter();
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03 };

        await adapter.SaveAsync("rooms/2026/11/photo-1/original.jpg", new MemoryStream(content));

        var savedPath = Path.Combine(_rootPath, "rooms", "2026", "11", "photo-1", "original.jpg");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(savedPath), Is.True);
            Assert.That(File.ReadAllBytes(savedPath), Is.EqualTo(content));
        });
    }

    [Test]
    public async Task SaveAsync_BackslashSeparatedKey_WritesTheSameNestedPath()
    {
        var adapter = CreateAdapter();

        await adapter.SaveAsync(@"rooms\2026\photo-1\original.jpg", new MemoryStream([0x01, 0x02]));

        Assert.That(
            File.Exists(Path.Combine(_rootPath, "rooms", "2026", "photo-1", "original.jpg")),
            Is.True);
    }

    [Test]
    public async Task SaveAsync_ExistingFile_OverwritesContent()
    {
        var adapter = CreateAdapter();
        await adapter.SaveAsync("rooms/p1/original.jpg", new MemoryStream([0x01, 0x02]));

        await adapter.SaveAsync("rooms/p1/original.jpg", new MemoryStream([0x03, 0x04, 0x05]));

        var savedPath = Path.Combine(_rootPath, "rooms", "p1", "original.jpg");
        Assert.That(File.ReadAllBytes(savedPath), Is.EqualTo(new byte[] { 0x03, 0x04, 0x05 }));
    }

    [Test]
    public async Task SaveAsync_RootPathWithTrailingSeparator_StillWritesUnderRoot()
    {
        var adapter = CreateAdapter(rootPath: _rootPath + Path.DirectorySeparatorChar);

        await adapter.SaveAsync("rooms/p1/original.jpg", new MemoryStream([0x01]));

        Assert.That(File.Exists(Path.Combine(_rootPath, "rooms", "p1", "original.jpg")), Is.True);
    }

    [Test]
    public async Task SaveAsync_KeySharingRootNameAsPrefix_StaysInsideRoot()
    {
        // Regression guard: a naive "root + key" concatenation would write "<root>evil.jpg" (root
        // "media" -> sibling-looking "mediaevil.jpg"); the resolved path must stay under the root.
        var adapter = CreateAdapter();

        await adapter.SaveAsync("evil.jpg", new MemoryStream([0x01]));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_rootPath, "evil.jpg")), Is.True);
            Assert.That(File.Exists(_rootPath + "evil.jpg"), Is.False);
        });
    }

    [TestCase("../evil.jpg")]
    [TestCase("rooms/../../evil.jpg")]
    [TestCase("rooms\\..\\..\\evil.jpg")]
    [TestCase("/etc/passwd")]
    [TestCase("C:/Windows/evil.jpg")]
    [TestCase("")]
    [TestCase("   ")]
    public async Task SaveAsync_KeyOutsideRootOrBlank_ThrowsWithoutWritingAnything(string storageKey)
    {
        var adapter = CreateAdapter();

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await adapter.SaveAsync(storageKey, new MemoryStream([0x01])));

        Assert.That(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        var adapter = CreateAdapter();
        await adapter.SaveAsync("rooms/p1/original.jpg", new MemoryStream([0x01]));
        var savedPath = Path.Combine(_rootPath, "rooms", "p1", "original.jpg");

        await adapter.DeleteAsync("rooms/p1/original.jpg");

        Assert.That(File.Exists(savedPath), Is.False);
    }

    [Test]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        var adapter = CreateAdapter();

        await adapter.DeleteAsync("rooms/missing/original.jpg");

        Assert.That(File.Exists(Path.Combine(_rootPath, "rooms", "missing", "original.jpg")), Is.False);
    }

    [Test]
    public async Task DeleteAsync_DeletedTwice_IsIdempotent()
    {
        var adapter = CreateAdapter();
        await adapter.SaveAsync("rooms/p1/original.jpg", new MemoryStream([0x01]));

        await adapter.DeleteAsync("rooms/p1/original.jpg");
        await adapter.DeleteAsync("rooms/p1/original.jpg");

        Assert.That(File.Exists(Path.Combine(_rootPath, "rooms", "p1", "original.jpg")), Is.False);
    }

    [Test]
    public void GetPublicUrl_ReturnsMediaUrlUnderConfiguredBaseUrl()
    {
        var adapter = CreateAdapter();

        var url = adapter.GetPublicUrl("rooms/2026/11/photo-1/original.jpg");

        Assert.That(url, Is.EqualTo("http://localhost:5000/media/rooms/2026/11/photo-1/original.jpg"));
    }

    [Test]
    public void GetPublicUrl_TrailingSlashInBaseUrl_IsTrimmed()
    {
        var adapter = CreateAdapter("https://api.example.com/");

        var url = adapter.GetPublicUrl("rooms/p1/thumb.jpg");

        Assert.That(url, Is.EqualTo("https://api.example.com/media/rooms/p1/thumb.jpg"));
    }

    [Test]
    public void GetPublicUrl_BackslashKey_AlwaysUsesForwardSlashes()
    {
        var adapter = CreateAdapter();

        var url = adapter.GetPublicUrl(@"rooms\2026\photo-1\original.jpg");

        Assert.That(url, Is.EqualTo("http://localhost:5000/media/rooms/2026/photo-1/original.jpg"));
    }

    [Test]
    public void GetPublicUrl_TraversalKey_Throws()
    {
        var adapter = CreateAdapter();

        Assert.Throws<ArgumentException>(() => adapter.GetPublicUrl("../secret.jpg"));
    }

    [Test]
    public void AddInfrastructure_RegistersBothMediaPortsWithTheSameSettingsInstance()
    {
        var settings = new MediaSettings
        {
            RootPath = _rootPath,
            PublicBaseUrl = "https://cdn.example.com/"
        };
        var services = new ServiceCollection();

        services.AddInfrastructure(mediaSettings: settings);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IPhotoStoragePort>();
        Assert.Multiple(() =>
        {
            Assert.That(storage, Is.InstanceOf<LocalDiskPhotoStorageAdapter>());
            Assert.That(
                provider.GetRequiredService<IImageProcessorPort>(),
                Is.InstanceOf<ByteLevelImageProcessorAdapter>());
            Assert.That(provider.GetRequiredService<MediaSettings>(), Is.SameAs(settings));
            Assert.That(
                storage.GetPublicUrl("rooms/p1/original.jpg"),
                Is.EqualTo("https://cdn.example.com/media/rooms/p1/original.jpg"));
        });
    }
}
