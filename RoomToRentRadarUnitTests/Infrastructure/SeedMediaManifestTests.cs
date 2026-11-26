using System.Text.Json;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// T00.10: the committed development seed photos are complete, within the NFR-03 size limits,
/// free of source metadata (BR-MED-04) and credited.
/// </summary>
[TestFixture]
public class SeedMediaManifestTests
{
    private const int ThumbLimitBytes = 60 * 1024;
    private const int MediumLimitBytes = 300 * 1024;

    private static readonly string[] Groups =
        ["BoardingRoom", "MiniApartment", "WholeHouse", "Dormitory", "Bathroom"];

    public sealed record ManifestPhoto(string Key, string Group, int Width, int Height, long MediumSizeBytes);

    public sealed record Manifest(List<ManifestPhoto> Photos);

    private static string SeedMediaPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "RoomToRentRadar.Api", "SeedMedia");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }
        }

        throw new AssertionException("RoomToRentRadar.Api/SeedMedia/manifest.json was not found.");
    }

    private static List<ManifestPhoto> LoadPhotos() =>
        JsonSerializer.Deserialize<Manifest>(
            File.ReadAllText(Path.Combine(SeedMediaPath(), "manifest.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Photos;

    private static string FilePath(string key, string rendition) =>
        Path.Combine(SeedMediaPath(), "rooms", $"{key}-{rendition}.jpg");

    // Walks the JPEG marker segments up to the start of scan and returns every application/comment marker.
    private static List<byte> MetadataMarkers(byte[] jpeg)
    {
        var markers = new List<byte>();
        var offset = 2;
        while (offset + 4 <= jpeg.Length && jpeg[offset] == 0xFF && jpeg[offset + 1] != 0xDA)
        {
            var marker = jpeg[offset + 1];
            if (marker is (>= 0xE1 and <= 0xEF) or 0xFE)
            {
                markers.Add(marker);
            }

            offset += 2 + ((jpeg[offset + 2] << 8) | jpeg[offset + 3]);
        }

        return markers;
    }

    [Test]
    public void Manifest_HasUniqueKeys_KnownGroups_AndAtLeastTwoPhotosPerGroup()
    {
        var photos = LoadPhotos();

        Assert.Multiple(() =>
        {
            Assert.That(photos.Select(p => p.Key), Is.Unique);
            Assert.That(photos.Select(p => p.Group).Distinct(), Is.EquivalentTo(Groups));
            foreach (var group in Groups)
            {
                Assert.That(photos.Count(p => p.Group == group), Is.GreaterThanOrEqualTo(2), group);
            }
        });
    }

    [Test]
    public void EveryRendition_IsAJpegWithinItsSizeLimit_WithoutMetadata()
    {
        Assert.Multiple(() =>
        {
            foreach (var photo in LoadPhotos())
            {
                foreach (var (rendition, limit) in new[] { ("thumb", ThumbLimitBytes), ("medium", MediumLimitBytes) })
                {
                    var name = $"{photo.Key}-{rendition}";
                    var bytes = File.ReadAllBytes(FilePath(photo.Key, rendition));
                    Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xFF, 0xD8, 0xFF }), name);
                    Assert.That(bytes.Length, Is.LessThanOrEqualTo(limit), name);
                    Assert.That(MetadataMarkers(bytes), Is.Empty, $"{name} carries metadata (EXIF/XMP/comment).");
                }
            }
        });
    }

    [Test]
    public void ManifestDimensionsAndSize_MatchTheMediumFile()
    {
        Assert.Multiple(() =>
        {
            foreach (var photo in LoadPhotos())
            {
                var path = FilePath(photo.Key, "medium");
                using var stream = File.OpenRead(path);
                Assert.That(ImageHeaderReader.TryReadSize(stream, out var width, out var height), Is.True, photo.Key);
                Assert.That((width, height), Is.EqualTo((photo.Width, photo.Height)), photo.Key);
                Assert.That(new FileInfo(path).Length, Is.EqualTo(photo.MediumSizeBytes), photo.Key);
            }
        });
    }

    [Test]
    public void EveryPhoto_IsCreditedWithItsSourcePage()
    {
        var lines = File.ReadAllLines(Path.Combine(SeedMediaPath(), "CREDITS.md"));

        Assert.Multiple(() =>
        {
            foreach (var photo in LoadPhotos())
            {
                Assert.That(
                    lines.Any(l => l.Contains($"`{photo.Key}`") && l.Contains("https://www.pexels.com/photo/")),
                    Is.True,
                    photo.Key);
            }
        });
    }

    [Test]
    public void RenditionFolder_HasNoUnlistedFiles()
    {
        var expected = LoadPhotos()
            .SelectMany(p => new[] { $"{p.Key}-thumb.jpg", $"{p.Key}-medium.jpg" });

        var actual = Directory.GetFiles(Path.Combine(SeedMediaPath(), "rooms")).Select(Path.GetFileName);

        Assert.That(actual, Is.EquivalentTo(expected));
    }
}
