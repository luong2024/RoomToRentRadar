using RoomToRentRadar.Helpers;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.ViewModels;

public sealed class GalleryPhotoItem
{
    private const string DefaultAccessibleDescription = "Ảnh phòng";

    private ImageSource? _fullSource;
    private ImageSource? _thumbSource;

    public GalleryPhotoItem(ListingPhotoDto photo)
    {
        Id = photo.Id;
        Kind = photo.Kind;
        Alt = photo.Alt;
        Width = photo.Width;
        Height = photo.Height;
        IsCover = photo.IsCover;
        FullUrl = MediaUrlResolver.Resolve(photo.Url) ?? photo.Url;
        ThumbUrl = string.IsNullOrWhiteSpace(photo.ThumbUrl)
            ? FullUrl
            : MediaUrlResolver.Resolve(photo.ThumbUrl) ?? photo.ThumbUrl;
    }

    public Guid Id { get; }

    public ListingPhotoKind Kind { get; }

    public string? Alt { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsCover { get; }

    public string FullUrl { get; }

    public string ThumbUrl { get; }

    public bool IsPanorama => Kind == ListingPhotoKind.Panorama;

    public double AspectRatio => Height > 0 ? (double)Width / Height : 4d / 3d;

    public string AccessibleDescription => string.IsNullOrWhiteSpace(Alt) ? DefaultAccessibleDescription : Alt!;

    public ImageSource FullSource => _fullSource ??= CreateSource(FullUrl);

    public ImageSource ThumbSource => _thumbSource ??= CreateSource(ThumbUrl);

    private static ImageSource CreateSource(string url) => new UriImageSource
    {
        Uri = new Uri(url),
        CachingEnabled = true,
        CacheValidity = TimeSpan.FromDays(7)
    };
}
