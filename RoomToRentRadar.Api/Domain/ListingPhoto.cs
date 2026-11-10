using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Domain;

public sealed class ListingPhoto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbUrl { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ListingPhotoKind Kind { get; set; }
    public string? Alt { get; set; }
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
}
