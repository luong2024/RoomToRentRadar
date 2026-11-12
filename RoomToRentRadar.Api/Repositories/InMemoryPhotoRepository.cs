using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Photos;

namespace RoomToRentRadar.Api.Repositories;

/// <summary>Lưu và lấy danh sách ảnh trực tiếp từ các phòng trong IListingRepository.</summary>
public class InMemoryPhotoRepository(IListingRepository listingRepository) : IPhotoRepository
{
    private readonly Lock _sync = new();

    public InMemoryPhotoRepository()
        : this(new InMemoryListingRepository())
    {
    }

    public async Task<IReadOnlyList<PhotoDto>?> GetPhotosAsync(string listingId, CancellationToken ct)
    {
        var doc = await listingRepository.GetByIdAsync(listingId, ct);
        if (doc is null) return null;

        lock (_sync)
        {
            return doc.Photos.OrderBy(p => p.Order).Select(ToDto).ToList();
        }
    }

    public async Task<PhotoAddResult?> AddPhotosAsync(string listingId, IReadOnlyList<PhotoDto> photos, CancellationToken ct)
    {
        var doc = await listingRepository.GetByIdAsync(listingId, ct);
        if (doc is null) return null;

        lock (_sync)
        {
            int next = doc.Photos.Count == 0 ? 1 : doc.Photos.Max(p => p.Order) + 1;
            var added = new List<PhotoDto>(photos.Count);
            foreach (var p in photos)
            {
                p.Order = next++;
                doc.Photos.Add(ToDocument(p));
                added.Add(p);
            }

            return new PhotoAddResult(added, doc.Photos.Count);
        }
    }

    private static PhotoDto ToDto(PhotoDocument p) => new()
    {
        Id = p.Id,
        Url = p.Url,
        ThumbnailUrl = p.ThumbnailUrl,
        Is360 = p.Is360,
        Order = p.Order,
        Width = p.Width,
        Height = p.Height,
        SizeBytes = p.SizeBytes
    };

    private static PhotoDocument ToDocument(PhotoDto p) => new()
    {
        Id = p.Id,
        Url = p.Url,
        ThumbnailUrl = p.ThumbnailUrl,
        Is360 = p.Is360,
        Order = p.Order,
        Width = p.Width,
        Height = p.Height,
        SizeBytes = p.SizeBytes
    };
}
