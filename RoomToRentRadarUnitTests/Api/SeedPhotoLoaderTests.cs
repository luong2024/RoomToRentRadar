using System.Text.Json;
using RoomToRentRadar.Api.Configuration;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>T00.10: validation of the Development seed photo folder.</summary>
[TestFixture]
public class SeedPhotoLoaderTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"rtr_seed_media_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_folder, "rooms"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private static object Entry(string key, string group) =>
        new { key, group, width = 1280, height = 853, mediumSizeBytes = 1000 };

    private static List<object> ValidEntries()
    {
        var entries = new List<object>();
        foreach (var type in Enum.GetNames<RoomType>())
        {
            entries.Add(Entry($"{type.ToLowerInvariant()}-1", type));
            entries.Add(Entry($"{type.ToLowerInvariant()}-2", type));
        }

        entries.Add(Entry("bathroom-1", "Bathroom"));
        return entries;
    }

    private void Write(IEnumerable<object> entries, bool createFiles = true)
    {
        var json = JsonSerializer.Serialize(new { photos = entries });
        File.WriteAllText(Path.Combine(_folder, "manifest.json"), json);
        if (!createFiles)
        {
            return;
        }

        using var document = JsonDocument.Parse(json);
        foreach (var photo in document.RootElement.GetProperty("photos").EnumerateArray())
        {
            var key = photo.GetProperty("key").GetString()!;
            if (key.All(c => char.IsLetterOrDigit(c) || c == '-'))
            {
                File.WriteAllBytes(Path.Combine(_folder, "rooms", $"{key}-thumb.jpg"), [0xFF]);
                File.WriteAllBytes(Path.Combine(_folder, "rooms", $"{key}-medium.jpg"), [0xFF]);
            }
        }
    }

    [Test]
    public void Load_ValidFolder_ReturnsStorageKeysAndGroups()
    {
        Write(ValidEntries());

        var photos = SeedPhotoLoader.Load(_folder);

        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(9));
            Assert.That(photos.Count(p => p.RoomType is null), Is.EqualTo(1));
            Assert.That(photos[0].ThumbPath, Is.EqualTo("seed/rooms/boardingroom-1-thumb.jpg"));
            Assert.That(photos[0].MediumPath, Is.EqualTo("seed/rooms/boardingroom-1-medium.jpg"));
        });
    }

    [Test]
    public void Load_CommittedSeedMedia_IsValid()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "RoomToRentRadar.Api", "SeedMedia", "manifest.json")))
        {
            directory = directory.Parent;
        }

        var photos = SeedPhotoLoader.Load(Path.Combine(directory!.FullName, "RoomToRentRadar.Api", "SeedMedia"));

        Assert.That(photos, Has.Count.GreaterThanOrEqualTo(18));
    }

    [Test]
    public void Load_NoManifest_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SeedPhotoLoader.Load(_folder));
    }

    [Test]
    public void Load_MissingImageFile_Throws()
    {
        Write(ValidEntries(), createFiles: false);

        var ex = Assert.Throws<InvalidOperationException>(() => SeedPhotoLoader.Load(_folder));
        Assert.That(ex!.Message, Does.Contain("missing"));
    }

    [TestCase("../secret", "Bathroom")]
    [TestCase("Upper-Case", "Bathroom")]
    [TestCase("ok-key", "Garage")]
    [TestCase("ok-key", "1")]
    public void Load_InvalidKeyOrGroup_Throws(string key, string group)
    {
        var entries = ValidEntries();
        entries.Add(Entry(key, group));
        Write(entries);

        Assert.Throws<InvalidOperationException>(() => SeedPhotoLoader.Load(_folder));
    }

    [Test]
    public void Load_RoomTypeWithOnePhoto_Throws()
    {
        var entries = ValidEntries();
        entries.RemoveAt(0);
        Write(entries);

        Assert.Throws<InvalidOperationException>(() => SeedPhotoLoader.Load(_folder));
    }

    [Test]
    public void Load_NoSupplementaryPhoto_Throws()
    {
        var entries = ValidEntries();
        entries.RemoveAt(entries.Count - 1);
        Write(entries);

        Assert.Throws<InvalidOperationException>(() => SeedPhotoLoader.Load(_folder));
    }
}
