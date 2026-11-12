using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Storage;

public sealed class LocalDiskPhotoStorageAdapter : IPhotoStoragePort
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalDiskPhotoStorageAdapter(string rootPath, string publicBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(publicBaseUrl);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Photo storage root path is required.", nameof(rootPath));
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            throw new ArgumentException("Photo public base URL is required.", nameof(publicBaseUrl));
        }

        _rootPath = Path.GetFullPath(rootPath);
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // Stream straight to disk: uploads can be non-seekable and photos are too large to buffer in memory.
        await using var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        await content.CopyToAsync(fileStream, ct);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = ResolvePath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Photo path is required.", nameof(path));
        }

        var normalized = path.Replace('\\', '/').TrimStart('/');
        return $"{_publicBaseUrl}/media/{normalized}";
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Photo path is required.", nameof(path));
        }

        if (Path.IsPathRooted(path))
        {
            throw new InvalidOperationException("Photo path must be relative.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, path));

        // Traversal guard: a relative key like "../evil.jpg" must never resolve outside the media root.
        if (!IsWithin(_rootPath, fullPath))
        {
            throw new InvalidOperationException("Resolved photo path escapes the media root.");
        }

        return fullPath;
    }

    private static bool IsWithin(string basePath, string candidatePath)
    {
        var basePrefix = basePath.EndsWith(Path.DirectorySeparatorChar)
            ? basePath
            : basePath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
