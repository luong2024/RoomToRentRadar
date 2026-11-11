using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Options;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Photos;

namespace RoomToRentRadar.Api.Services;

public partial class PhotoService(
    IListingRepository listings,
    IPhotoRepository photos,
    IImageProcessor imageProcessor,
    IPhotoStorage storage,
    IOptions<PhotoOptions> options,
    ILogger<PhotoService> logger) : IPhotoService
{
    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png"];
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png"];

    // roomId được dùng làm tên thư mục -> chỉ cho phép ký tự an toàn.
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeIdRegex();

    private readonly PhotoOptions _opt = options.Value;

    public async Task<UploadPhotosResponse> UploadAsync(string roomId, UploadPhotosRequest request, string baseUrl, CancellationToken cancellationToken = default)
    {
        await EnsureRoomExistsAsync(roomId, cancellationToken);

        // ---- Validate toàn bộ request trước khi xử lý ----
        var files = request.Files;
        if (files.Count == 0)
            throw new PhotoValidationException("Chưa có tệp ảnh nào được gửi lên (field 'files').");
        if (files.Count > _opt.MaxFilesPerRequest)
            throw new PhotoValidationException($"Tối đa {_opt.MaxFilesPerRequest} ảnh mỗi lần tải lên.");

        foreach (var f in files)
        {
            if (f.Length == 0)
                throw new PhotoValidationException($"Tệp '{f.FileName}' rỗng.");
            if (f.Length > _opt.MaxFileSizeBytes)
                throw new PhotoValidationException($"Tệp '{f.FileName}' vượt quá {_opt.MaxFileSizeBytes / 1024 / 1024} MB.");
            if (!AllowedContentTypes.Contains(f.ContentType.ToLowerInvariant())
                || !AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant()))
                throw new PhotoValidationException($"Tệp '{f.FileName}' không phải ảnh JPEG/PNG.");
        }

        // ---- Nén toàn bộ ảnh trong RAM trước: có 1 ảnh lỗi thì không lưu gì cả ----
        var processed = new List<ProcessedImage>(files.Count);
        foreach (var f in files)
        {
            await using var stream = f.OpenReadStream();
            try
            {
                processed.Add(await imageProcessor.ProcessAsync(stream, request.Is360, cancellationToken));
            }
            catch (PhotoValidationException ex)
            {
                throw new PhotoValidationException($"Tệp '{f.FileName}': {ex.Message}");
            }
        }

        // ---- Lưu tệp vật lý, rồi ghi metadata; lỗi DB thì dọn tệp đã ghi ----
        var savedPaths = new List<string>();
        var entities = new List<RoomPhoto>();
        try
        {
            foreach (var img in processed)
            {
                var id = Guid.NewGuid().ToString("N");
                var path = await storage.SaveAsync(roomId, id + img.Extension, img.Content, cancellationToken);
                savedPaths.Add(path);

                entities.Add(new RoomPhoto
                {
                    Id = id,
                    RoomId = roomId,
                    StoragePath = path,
                    Is360 = request.Is360,
                    Width = img.Width,
                    Height = img.Height,
                    SizeBytes = img.Content.LongLength,
                    ContentType = img.ContentType
                });
            }

            await photos.AddRangeAsync(entities, cancellationToken);
        }
        catch
        {
            foreach (var p in savedPaths)
            {
                try { await storage.DeleteAsync(p, CancellationToken.None); }
                catch (Exception ex) { logger.LogWarning(ex, "Không dọn được tệp {Path}", p); }
            }
            throw;
        }

        logger.LogInformation("Uploaded {Count} photo(s) for room {RoomId}", entities.Count, roomId);
        var dtos = entities.Select(e => ToDto(e, baseUrl)).ToList();
        return new UploadPhotosResponse(roomId, dtos.Count, dtos);
    }

    public async Task<PhotoGalleryResponse> GetGalleryAsync(string roomId, string baseUrl, CancellationToken cancellationToken = default)
    {
        await EnsureRoomExistsAsync(roomId, cancellationToken);
        var items = await photos.GetByRoomAsync(roomId, cancellationToken);
        var dtos = items.Select(p => ToDto(p, baseUrl)).ToList();
        return new PhotoGalleryResponse(roomId, dtos.Count, dtos);
    }

    private async Task EnsureRoomExistsAsync(string roomId, CancellationToken ct)
    {
        if (!SafeIdRegex().IsMatch(roomId))
            throw new PhotoValidationException("RoomId không hợp lệ.");
        if (!await listings.ExistsAsync(roomId, ct))
            throw new RoomNotFoundException(roomId);
    }

    private PhotoDto ToDto(RoomPhoto p, string baseUrl) => new(
        p.Id, p.RoomId, storage.GetPublicUrl(p.StoragePath, baseUrl),
        p.Is360, p.Width, p.Height, p.SizeBytes, p.UploadedAt);
}
