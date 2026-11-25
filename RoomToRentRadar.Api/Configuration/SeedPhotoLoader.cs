using System.Text.Json;
using System.Text.RegularExpressions;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Api.Configuration;

/// <summary>
/// Reads the committed Development seed photos (<c>SeedMedia/manifest.json</c>, T00.10) and validates
/// them at start-up so a broken folder fails loudly instead of seeding dead image links.
/// </summary>
public static class SeedPhotoLoader
{
    public const string FolderName = "SeedMedia";
    public const string ImagesFolderName = "rooms";
    public const string SupplementaryGroup = "Bathroom";

    /// <summary>Request path of the images; storage keys below it resolve under <c>/media</c> (ADR-008).</summary>
    public const string RequestPath = "/media/seed/" + ImagesFolderName;

    private const string StorageKeyPrefix = "seed/" + ImagesFolderName + "/";

    // Two shots of the room's own type plus one supplementary shot give BR-MED-01's three photos.
    private const int MinimumPerRoomType = 2;

    // Keys become file names and URLs, so nothing that could leave the folder is accepted.
    private static readonly Regex KeyPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private sealed record ManifestPhoto(string? Key, string? Group, int Width, int Height, long MediumSizeBytes);

    private sealed record Manifest(List<ManifestPhoto>? Photos);

    public static IReadOnlyList<SeedPhoto> Load(string seedMediaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedMediaPath);

        var manifestPath = Path.Combine(seedMediaPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Seed photo manifest not found: '{manifestPath}'.");
        }

        var manifest = JsonSerializer.Deserialize<Manifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var photos = (manifest?.Photos ?? []).Select(entry => ToSeedPhoto(entry, seedMediaPath)).ToList();

        foreach (var roomType in Enum.GetValues<RoomType>())
        {
            if (photos.Count(p => p.RoomType == roomType) < MinimumPerRoomType)
            {
                throw new InvalidOperationException(
                    $"Seed photo manifest needs at least {MinimumPerRoomType} photos for '{roomType}'.");
            }
        }

        if (photos.All(p => p.RoomType is not null))
        {
            throw new InvalidOperationException($"Seed photo manifest needs a '{SupplementaryGroup}' photo.");
        }

        return photos;
    }

    private static SeedPhoto ToSeedPhoto(ManifestPhoto entry, string seedMediaPath)
    {
        var key = entry.Key ?? string.Empty;
        if (!KeyPattern.IsMatch(key))
        {
            throw new InvalidOperationException($"Seed photo key '{key}' is invalid.");
        }

        if (entry.Width <= 0 || entry.Height <= 0 || entry.MediumSizeBytes <= 0)
        {
            throw new InvalidOperationException($"Seed photo '{key}' has no valid dimensions or size.");
        }

        foreach (var fileName in new[] { $"{key}-thumb.jpg", $"{key}-medium.jpg" })
        {
            if (!File.Exists(Path.Combine(seedMediaPath, ImagesFolderName, fileName)))
            {
                throw new InvalidOperationException($"Seed photo file '{fileName}' is missing.");
            }
        }

        return new SeedPhoto(
            ParseGroup(entry.Group, key),
            ThumbPath: $"{StorageKeyPrefix}{key}-thumb.jpg",
            MediumPath: $"{StorageKeyPrefix}{key}-medium.jpg",
            entry.Width,
            entry.Height,
            entry.MediumSizeBytes);
    }

    private static RoomType? ParseGroup(string? group, string key)
    {
        if (string.Equals(group, SupplementaryGroup, StringComparison.Ordinal))
        {
            return null;
        }

        // Exact names only: Enum.TryParse alone would also accept numbers.
        if (group is not null && Enum.GetNames<RoomType>().Contains(group, StringComparer.Ordinal))
        {
            return Enum.Parse<RoomType>(group);
        }

        throw new InvalidOperationException($"Seed photo '{key}' has an unknown group '{group}'.");
    }
}
