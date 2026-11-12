using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public static class TestListingsSeed
{
    public const int PublishedCount = 20;
    public const int TotalCount = 25;

    public static Guid IdFor(int index) => new($"00000000-0000-0000-0000-{index:D12}");

    public static async Task SeedAsync(AppDbContext dbContext)
    {
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < TotalCount; index++)
        {
            var status = index switch
            {
                20 or 21 => ListingStatus.Draft,
                22 or 23 => ListingStatus.Hidden,
                24 => ListingStatus.Removed,
                _ => ListingStatus.Published
            };

            var listing = new Listing
            {
                Id = IdFor(index),
                Title = $"Test listing {index}",
                Description = $"Test listing {index} description",
                Price = 5_000_000L + index % 4 * 1_000_000L,
                AreaM2 = 30 + index % 5 * 5,
                Bedrooms = index % 4,
                RoomType = (RoomType)(index % 5),
                Address = $"Test address {index}, District {index % 3}",
                Lat = 10.7 + index * 0.001,
                Lng = 106.7 + index * 0.001,
                Status = status,
                CreatedAt = baseDate.AddHours(-(index % 6)),
                UpdatedAt = baseDate.AddHours(-(index % 6)).AddMinutes(5)
            };

            if (index == 0)
            {
                listing.Photos.Add(CreatePhoto(index, 1, "cover-special.jpg", isCover: true));
                listing.Photos.Add(CreatePhoto(index, 0, "photo-0.jpg", isCover: false));
            }
            else if (index == 1)
            {
                // Deliberately has no photos.
            }
            else if (index == 2)
            {
                listing.Photos.Add(CreatePhoto(index, 2, "photo-2.jpg", isCover: false));
                listing.Photos.Add(CreatePhoto(index, 1, "photo-1.jpg", isCover: false));
            }
            else
            {
                listing.Photos.Add(CreatePhoto(index, 0, $"cover-{index}.jpg", isCover: true));
            }

            if (index == 0)
            {
                // Added out of sort order so the detail endpoint has to order both lists.
                listing.Amenities.Add(CreateAmenity(index, AmenityCode.Parking, 1));
                listing.Amenities.Add(CreateAmenity(index, AmenityCode.Wifi, 0));
                listing.Rules.Add(CreateRule(index, 1, "No pets"));
                listing.Rules.Add(CreateRule(index, 0, "No smoking"));
            }
            else if (index == 1)
            {
                // Deliberately has no amenities or rules.
            }
            else if (index == 2)
            {
                listing.Amenities.Add(CreateAmenity(index, AmenityCode.AirConditioner, 0));
                listing.Rules.Add(CreateRule(index, 0, "No smoking"));
            }

            dbContext.Listings.Add(listing);
        }

        await dbContext.SaveChangesAsync();
    }

    public static async Task<IReadOnlyList<Guid>> PublishedIdsAsync(AppDbContext dbContext)
        => await dbContext.Listings
            .Where(x => x.Status == ListingStatus.Published)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync();

    /// <summary>
    /// Adds <paramref name="count"/> cheap published listings so the total count cap
    /// can be exercised. Ids start at index 1,000 to stay clear of <see cref="IdFor"/>.
    /// </summary>
    public static async Task SeedBulkAsync(AppDbContext dbContext, int count)
    {
        var baseDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < count; index++)
        {
            dbContext.Listings.Add(new Listing
            {
                Id = IdFor(1_000 + index),
                Title = $"Bulk listing {index}",
                Description = $"Bulk listing {index} description",
                Price = 5_000_000L + index % 4 * 1_000_000L,
                AreaM2 = 30 + index % 5 * 5,
                Bedrooms = index % 4,
                RoomType = (RoomType)(index % 5),
                Address = $"Bulk address {index}",
                Lat = 10.7 + index % 40 * 0.001,
                Lng = 106.7 + index % 40 * 0.001,
                Status = ListingStatus.Published,
                CreatedAt = baseDate.AddSeconds(-index),
                UpdatedAt = baseDate.AddSeconds(-index)
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static ListingPhoto CreatePhoto(int listingIndex, int sortOrder, string fileName, bool isCover)
        => new()
        {
            Id = new Guid($"11111111-0000-0000-0000-{(listingIndex * 100 + sortOrder):D12}"),
            ListingId = IdFor(listingIndex),
            Url = $"/media/test/{fileName}",
            ThumbUrl = $"/media/test/thumbs/{fileName}",
            Width = 1600,
            Height = 1067,
            Kind = ListingPhotoKind.Photo,
            Alt = $"Photo {sortOrder} for test listing {listingIndex}",
            SortOrder = sortOrder,
            IsCover = isCover
        };

    private static ListingAmenity CreateAmenity(int listingIndex, AmenityCode code, int sortOrder)
        => new()
        {
            Id = new Guid($"22222222-0000-0000-0000-{(listingIndex * 100 + sortOrder):D12}"),
            ListingId = IdFor(listingIndex),
            Code = code,
            SortOrder = sortOrder
        };

    private static ListingRule CreateRule(int listingIndex, int sortOrder, string text)
        => new()
        {
            Id = new Guid($"33333333-0000-0000-0000-{(listingIndex * 100 + sortOrder):D12}"),
            ListingId = IdFor(listingIndex),
            Text = text,
            SortOrder = sortOrder
        };
}
