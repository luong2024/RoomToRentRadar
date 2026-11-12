using Microsoft.Extensions.Options;

namespace RoomToRentRadar.Api.Services.Photos;

public class LocalPhotoStorage : IPhotoStorage
{
    public const string RequestPath = "/media";
    private readonly string _root;

    public LocalPhotoStorage(IOptions<PhotoOptions> options, IWebHostEnvironment env)
    {
        _root = ResolveRoot(options.Value, env.ContentRootPath);
        Directory.CreateDirectory(_root);
    }

    public static string ResolveRoot(PhotoOptions options, string contentRoot) =>
        Path.GetFullPath(Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(contentRoot, options.StoragePath));

    public async Task<string> SaveAsync(string listingId, string fileName, byte[] content, CancellationToken ct)
    {
        string path = SafePath(listingId, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
        return $"{RequestPath}/listings/{listingId}/{fileName}";
    }

    public Task DeleteAsync(string listingId, string fileName, CancellationToken ct)
    {
        string path = SafePath(listingId, fileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string SafePath(string listingId, string fileName)
    {
        string full = Path.GetFullPath(Path.Combine(_root, "listings", listingId, fileName));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new PhotoValidationException("Đường dẫn lưu trữ không hợp lệ.");
        return full;
    }
}
