using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

/// <summary>
/// Deterministic spatial fixture. The reference area is the rectangle
/// <c>(106.68, 10.75)</c> to <c>(106.72, 10.79)</c>; every row is placed so that a
/// test can tell inside, boundary, vertex and outside rows apart.
/// </summary>
public static class SpatialListingsSeed
{
    public const double MinLng = 106.68;
    public const double MinLat = 10.75;
    public const double MaxLng = 106.72;
    public const double MaxLat = 10.79;

    /// <summary>Rows 0-8 and 14 match the reference rectangle, published only.</summary>
    public const int RectangleMatchCount = 10;

    public static readonly Guid CenterId = IdFor(0);
    public static readonly Guid CenterBId = IdFor(1);
    public static readonly Guid CenterCId = IdFor(2);
    public static readonly Guid CenterDId = IdFor(3);
    public static readonly Guid DuplicateKeyOneId = IdFor(4);
    public static readonly Guid DuplicateKeyTwoId = IdFor(5);
    public static readonly Guid EdgeWestId = IdFor(6);
    public static readonly Guid VertexNorthWestId = IdFor(7);
    public static readonly Guid VertexSouthEastId = IdFor(8);
    public static readonly Guid OutsideNorthId = IdFor(9);
    public static readonly Guid OutsideWestId = IdFor(10);
    public static readonly Guid DraftInsideId = IdFor(11);
    public static readonly Guid HiddenInsideId = IdFor(12);
    public static readonly Guid RemovedInsideId = IdFor(13);

    /// <summary>Inside the reference rectangle but inside the notch of <see cref="ConcaveRing"/>.</summary>
    public static readonly Guid NotchInsideId = IdFor(14);

    public static Guid IdFor(int index) => new($"a1000000-0000-4000-8000-{index:D12}");

    public static double[][] RectangleRing() =>
    [
        [MinLng, MinLat],
        [MaxLng, MinLat],
        [MaxLng, MaxLat],
        [MinLng, MaxLat],
        [MinLng, MinLat]
    ];

    /// <summary>
    /// L-shaped ring whose notch is the box <c>lng 106.68-106.70</c>, <c>lat 10.77-10.79</c>.
    /// </summary>
    public static double[][] ConcaveRing() =>
    [
        [MinLng, MinLat],
        [MaxLng, MinLat],
        [MaxLng, MaxLat],
        [106.70, MaxLat],
        [106.70, 10.77],
        [MinLng, 10.77],
        [MinLng, MinLat]
    ];

    public static async Task SeedAsync(AppDbContext dbContext)
    {
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var center = Add(dbContext, 0, "Spatial center", 10.770, 106.700, ListingStatus.Published, baseDate, 5_000_000, 30, 1);
        center.Photos.Add(Photo(0, 0, "center.jpg", isCover: true));

        // Cover without a thumbnail: the response falls back to the full url.
        var centerB = Add(dbContext, 1, "Spatial center b", 10.772, 106.702, ListingStatus.Published, baseDate.AddDays(1), 7_000_000, 45, 2);
        centerB.Photos.Add(Photo(1, 0, "center-b.jpg", isCover: true, withThumbnail: false));

        // No photos at all.
        Add(dbContext, 2, "Spatial center c", 10.768, 106.698, ListingStatus.Published, baseDate.AddDays(2), 9_000_000, 60, 2);

        // No cover flag: the lowest sort order wins. Shares its sort keys with rows 4 and 5.
        var centerD = Add(dbContext, 3, "Spatial center d", 10.780, 106.710, ListingStatus.Published, baseDate.AddDays(3), 9_000_000, 60, 2);
        centerD.Photos.Add(Photo(3, 1, "center-d-1.jpg", isCover: false));
        centerD.Photos.Add(Photo(3, 0, "center-d-0.jpg", isCover: false));

        var duplicateOne = Add(dbContext, 4, "Spatial duplicate one", 10.781, 106.711, ListingStatus.Published, baseDate.AddDays(3), 9_000_000, 60, 2);
        duplicateOne.Photos.Add(Photo(4, 0, "duplicate-one.jpg", isCover: true));

        var duplicateTwo = Add(dbContext, 5, "Spatial duplicate two", 10.782, 106.712, ListingStatus.Published, baseDate.AddDays(3), 9_000_000, 60, 2);
        duplicateTwo.Photos.Add(Photo(5, 0, "duplicate-two.jpg", isCover: true));

        // Boundary rows: one on the west edge, one on each of two vertices.
        var edgeWest = Add(dbContext, 6, "Spatial edge west", 10.770, MinLng, ListingStatus.Published, baseDate.AddDays(4), 5_000_000, 30, 1);
        edgeWest.Photos.Add(Photo(6, 0, "edge-west.jpg", isCover: true));

        var vertexNorthWest = Add(dbContext, 7, "Spatial vertex north west", MaxLat, MinLng, ListingStatus.Published, baseDate.AddDays(5), 5_000_000, 30, 1);
        vertexNorthWest.Photos.Add(Photo(7, 0, "vertex-north-west.jpg", isCover: true));

        var vertexSouthEast = Add(dbContext, 8, "Spatial vertex south east", MinLat, MaxLng, ListingStatus.Published, baseDate.AddDays(6), 5_000_000, 30, 1);
        vertexSouthEast.Photos.Add(Photo(8, 0, "vertex-south-east.jpg", isCover: true));

        Add(dbContext, 9, "Spatial outside north", 10.7901, 106.700, ListingStatus.Published, baseDate.AddDays(7), 5_000_000, 30, 1)
            .Photos.Add(Photo(9, 0, "outside-north.jpg", isCover: true));

        Add(dbContext, 10, "Spatial outside west", 10.770, 106.6799, ListingStatus.Published, baseDate.AddDays(8), 5_000_000, 30, 1)
            .Photos.Add(Photo(10, 0, "outside-west.jpg", isCover: true));

        Add(dbContext, 14, "Spatial notch inside", 10.780, 106.690, ListingStatus.Published, baseDate.AddDays(12), 5_000_000, 30, 3)
            .Photos.Add(Photo(14, 0, "notch-inside.jpg", isCover: true));

        Add(dbContext, 11, "Spatial draft inside", 10.770, 106.700, ListingStatus.Draft, baseDate.AddDays(9), 5_000_000, 30, 1);
        Add(dbContext, 12, "Spatial hidden inside", 10.770, 106.700, ListingStatus.Hidden, baseDate.AddDays(10), 5_000_000, 30, 1);
        Add(dbContext, 13, "Spatial removed inside", 10.770, 106.700, ListingStatus.Removed, baseDate.AddDays(11), 5_000_000, 30, 1);

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Adds <paramref name="count"/> cheap published listings inside the reference
    /// rectangle. Used by the total count cap test.
    /// </summary>
    public static async Task SeedManyInsideAsync(AppDbContext dbContext, int count)
    {
        var baseDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < count; index++)
        {
            var lat = MinLat + 0.001 + index % 30 * 0.001;
            var lng = MinLng + 0.001 + index / 30 % 30 * 0.001;

            dbContext.Listings.Add(new Listing
            {
                Id = IdFor(1_000 + index),
                Title = $"Bulk listing {index}",
                Description = $"Bulk listing {index} description",
                Price = 5_000_000,
                AreaM2 = 30,
                Bedrooms = 1,
                Address = $"Bulk address {index}",
                Lat = lat,
                Lng = lng,
                Status = ListingStatus.Published,
                CreatedAt = baseDate.AddSeconds(-index),
                UpdatedAt = baseDate.AddSeconds(-index)
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static Listing Add(
        AppDbContext dbContext,
        int index,
        string title,
        double lat,
        double lng,
        ListingStatus status,
        DateTime createdAt,
        long price,
        double areaM2,
        int bedrooms)
    {
        var listing = new Listing
        {
            Id = IdFor(index),
            Title = title,
            Description = $"{title} description",
            Price = price,
            AreaM2 = areaM2,
            Bedrooms = bedrooms,
            Address = $"{title} address",
            Lat = lat,
            Lng = lng,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddHours(1)
        };

        dbContext.Listings.Add(listing);
        return listing;
    }

    private static ListingPhoto Photo(
        int listingIndex,
        int sortOrder,
        string fileName,
        bool isCover,
        bool withThumbnail = true)
        => new()
        {
            Id = new Guid($"b1000000-0000-4000-8000-{(listingIndex * 100 + sortOrder):D12}"),
            ListingId = IdFor(listingIndex),
            Url = $"/media/spatial/{fileName}",
            ThumbUrl = withThumbnail ? $"/media/spatial/thumbs/{fileName}" : null,
            Width = 1200,
            Height = 800,
            Kind = ListingPhotoKind.Photo,
            Alt = fileName,
            SortOrder = sortOrder,
            IsCover = isCover
        };
}
