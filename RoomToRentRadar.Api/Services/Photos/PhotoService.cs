using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Photos;

namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>Một tệp client gửi lên (đã tách khỏi IFormFile để service dễ test).</summary>
public record PhotoUploadItem(string FileName, long Length, Stream Content, bool Is360);

public interface IPhotoService
{
    Task<UploadPhotosResponse> UploadAsync(string listingId, IReadOnlyList<PhotoUploadItem> files, CancellationToken ct);
    Task<PhotoGalleryResponse> GetGalleryAsync(string listingId, CancellationToken ct);
}

public partial class PhotoService(
    IPhotoRepository repository,
    IPhotoStorage storage,
    IImageProcessor processor,
    IOptions<PhotoOptions> options) : IPhotoService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];
    private readonly PhotoOptions _o = options.Value;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex SafeId();

    public async Task<UploadPhotosResponse> UploadAsync(
        string listingId, IReadOnlyList<PhotoUploadItem> files, CancellationToken ct)
    {
        if (!SafeId().IsMatch(listingId))
            throw new PhotoValidationException("Mã phòng không hợp lệ.");
        if (files.Count == 0)
            throw new PhotoValidationException("Chưa có tệp ảnh nào được gửi lên.");
        if (files.Count > _o.MaxFilesPerRequest)
            throw new PhotoValidationException($"Tối đa {_o.MaxFilesPerRequest} ảnh cho mỗi lần tải lên.");

        var existing = await repository.GetPhotosAsync(listingId, ct) ?? throw new ListingNotFoundException(listingId);
        if (existing.Count + files.Count > _o.MaxPhotosPerListing)
            throw new PhotoValidationException($"Mỗi phòng tối đa {_o.MaxPhotosPerListing} ảnh (hiện có {existing.Count}).");

        var processed = new List<(PhotoUploadItem Item, ProcessedImage Image)>(files.Count);
        foreach (var f in files)
        {
            ValidateFile(f);
            using var ms = new MemoryStream();
            await f.Content.CopyToAsync(ms, ct);
            processed.Add((f, await processor.ProcessAsync(ms.ToArray(), f.FileName, f.Is360, ct)));
        }

        var saved = new List<string>();
        var dtos = new List<PhotoDto>(processed.Count);
        try
        {
            foreach (var (item, img) in processed)
            {
                string id = Guid.NewGuid().ToString("N");
                string main = $"{id}.jpg", thumb = $"{id}_thumb.jpg";
                string url = await storage.SaveAsync(listingId, main, img.Content, ct); saved.Add(main);
                string thumbUrl = await storage.SaveAsync(listingId, thumb, img.Thumbnail, ct); saved.Add(thumb);

                dtos.Add(new PhotoDto
                {
                    Id = id, Url = url, ThumbnailUrl = thumbUrl, Is360 = item.Is360,
                    Width = img.Width, Height = img.Height, SizeBytes = img.Content.Length
                });
            }

            var result = await repository.AddPhotosAsync(listingId, dtos, ct) ?? throw new ListingNotFoundException(listingId);
            return new UploadPhotosResponse
            {
                ListingId = listingId,
                UploadedCount = result.Added.Count,
                TotalPhotos = result.TotalPhotos,
                Photos = result.Added
            };
        }
        catch
        {
            foreach (var name in saved) await storage.DeleteAsync(listingId, name, CancellationToken.None);
            throw;
        }
    }

    public async Task<PhotoGalleryResponse> GetGalleryAsync(string listingId, CancellationToken ct)
    {
        if (!SafeId().IsMatch(listingId))
            throw new PhotoValidationException("Mã phòng không hợp lệ.");

        var photos = await repository.GetPhotosAsync(listingId, ct) ?? throw new ListingNotFoundException(listingId);
        return new PhotoGalleryResponse
        {
            ListingId = listingId,
            TotalPhotos = photos.Count,
            Total360Photos = photos.Count(p => p.Is360),
            Photos = photos
        };
    }

    private void ValidateFile(PhotoUploadItem f)
    {
        string ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new PhotoValidationException($"'{f.FileName}': chỉ chấp nhận .jpg, .jpeg, .png.");
        if (f.Length == 0)
            throw new PhotoValidationException($"'{f.FileName}' là tệp rỗng.");
        if (f.Length > _o.MaxFileSizeBytes)
            throw new PhotoValidationException(
                $"'{f.FileName}' vượt quá giới hạn {_o.MaxFileSizeBytes / (1024 * 1024)} MB.");
    }
}
