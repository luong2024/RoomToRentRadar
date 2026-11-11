using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Storage;

public sealed class LocalDiskPhotoStorageAdapter : IPhotoStoragePort
{
    private readonly string _rootPath;

    public LocalDiskPhotoStorageAdapter(string rootPath)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Uploads root path is required.", nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
    }

    public async Task<string> SaveAsync(string roomId, string fileName, Stream content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var (roomDirectory, fullPath) = ResolvePath(roomId, fileName);
        Directory.CreateDirectory(roomDirectory);

        await using (var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return $"/uploads/rooms/{roomId}/{fileName}";
    }

    public Task DeleteAsync(string roomId, string fileName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(fileName);

        var (_, fullPath) = ResolvePath(roomId, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private (string RoomDirectory, string FullPath) ResolvePath(string roomId, string fileName)
    {
        var roomsRoot = Path.GetFullPath(Path.Combine(_rootPath, "rooms"));
        var roomDirectory = Path.GetFullPath(Path.Combine(roomsRoot, roomId));
        var fullPath = Path.GetFullPath(Path.Combine(roomDirectory, fileName));

        // Reject paths that escape the uploads root, the rooms directory or the room directory
        // (e.g. roomId "../x" or fileName "../evil.jpg").
        if (!IsWithin(_rootPath, fullPath) ||
            !IsWithin(roomsRoot, roomDirectory) ||
            !IsWithin(roomDirectory, fullPath))
        {
            throw new InvalidOperationException("Resolved photo path escapes the uploads root.");
        }

        return (roomDirectory, fullPath);
    }

    private static bool IsWithin(string basePath, string candidatePath)
    {
        var basePrefix = basePath.EndsWith(Path.DirectorySeparatorChar)
            ? basePath
            : basePath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
