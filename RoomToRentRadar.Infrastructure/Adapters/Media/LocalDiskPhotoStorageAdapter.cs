using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Media;

/// <summary>
/// Stores listing photos under <see cref="MediaSettings.RootPath"/> and exposes them at
/// <c>{PublicBaseUrl}/media/{storageKey}</c> (ADR-008). Storage keys are relative, slash-separated
/// paths such as <c>rooms/2026/11/{photoId}/original.jpg</c>; path traversal is rejected.
/// </summary>
public sealed class LocalDiskPhotoStorageAdapter : IPhotoStoragePort
{
    private const string MediaUrlSegment = "/media";

    private readonly MediaSettings _settings;
    private readonly string _rootPath;

    public LocalDiskPhotoStorageAdapter(MediaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var rootPath = string.IsNullOrWhiteSpace(settings.RootPath) ? "media" : settings.RootPath;
        _settings = settings;
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous);
        await content.CopyToAsync(file, ct).ConfigureAwait(false);
        await file.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Deletes the file if it exists; deleting a missing key is a no-op (idempotent cleanup).</summary>
    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string storageKey)
    {
        var key = NormalizeKey(storageKey);
        var baseUrl = (_settings.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}{MediaUrlSegment}/{key}";
    }

    private string ResolvePath(string storageKey)
    {
        var key = NormalizeKey(storageKey);
        var fullPath = Path.GetFullPath(
            Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar)));

        // Compare relative to the root so a sibling that merely shares the root's name prefix (e.g.
        // root "media" vs "mediaX") can never be reached, even when the root is a drive/filesystem root.
        var relativePath = Path.GetRelativePath(_rootPath, fullPath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key escapes the media root directory.", nameof(storageKey));
        }

        return fullPath;
    }

    private static string NormalizeKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        var key = storageKey.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(key) ||
            key.StartsWith('/') ||
            key.Split('/').Any(segment => segment is ".." or ".") ||
            key.Contains(':'))
        {
            throw new ArgumentException("Storage key must be a relative path without traversal segments.", nameof(storageKey));
        }

        return key;
    }
}
