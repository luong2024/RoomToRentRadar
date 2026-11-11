using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Options;

namespace RoomToRentRadar.Api.Services.Photos;

public class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _root;
    private readonly string _requestPath;

    public LocalPhotoStorage(IOptions<PhotoOptions> options, IWebHostEnvironment env)
    {
        var o = options.Value;
        _root = Path.GetFullPath(Path.IsPathRooted(o.StorageRoot)
            ? o.StorageRoot
            : Path.Combine(env.ContentRootPath, o.StorageRoot));
        _requestPath = "/" + o.RequestPath.Trim('/');
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string roomId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var relative = $"rooms/{roomId}/{fileName}";
        var fullPath = ResolveSafe(relative);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return relative;
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storagePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string storagePath, string baseUrl)
        => $"{baseUrl.TrimEnd('/')}{_requestPath}/{storagePath}";

    /// <summary>Chặn path traversal: đường dẫn cuối cùng bắt buộc nằm trong thư mục gốc.</summary>
    private string ResolveSafe(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Đường dẫn lưu trữ không hợp lệ.");
        return full;
    }
}
