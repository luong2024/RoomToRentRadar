using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Data.Seed;

public static class DatabaseSeeder
{
    private const int SamplePublishedCount = 60;
    private const int SampleDraftCount = 4;
    private const int SampleHiddenCount = 3;
    private const int BenchmarkBatchSize = 1000;

    // One sample listing keeps an empty amenity and rule list to cover the empty detail case.
    private const int NoDetailIndex = 7;

    private static readonly (string District, string Address, double Lat, double Lng)[] Locations =
    [
        ("Quận 1", "123 Nguyễn Trãi", 10.7769, 106.7009),
        ("Quận 3", "45 Võ Văn Tần", 10.7840, 106.6847),
        ("Quận 7", "88 Nguyễn Thị Thập", 10.7340, 106.7218),
        ("Bình Thạnh", "12 Điện Biên Phủ", 10.8030, 106.7130),
        ("Phú Nhuận", "34 Phan Xích Long", 10.7990, 106.6800),
        ("Tân Bình", "56 Cộng Hòa", 10.8010, 106.6520),
        ("Gò Vấp", "90 Quang Trung", 10.8380, 106.6650),
        ("Thủ Đức", "17 Võ Văn Ngân", 10.8500, 106.7530)
    ];

    private static readonly string[] TitleTemplates =
    [
        "Căn hộ {Bedrooms}PN full nội thất tại {District}",
        "Studio ban công rộng gần trung tâm {District}",
        "Nhà nguyên căn {Bedrooms}PN hẻm xe hơi {District}",
        "Căn hộ dịch vụ cao cấp {District}",
        "Phòng trọ mới xây gần đại học {District}"
    ];

    private static readonly AmenityCode[] SampleAmenities =
    [
        AmenityCode.Wifi,
        AmenityCode.AirConditioner,
        AmenityCode.Parking,
        AmenityCode.WashingMachine,
        AmenityCode.Refrigerator,
        AmenityCode.Kitchen,
        AmenityCode.PrivateBathroom,
        AmenityCode.Balcony,
        AmenityCode.Elevator,
        AmenityCode.Security,
        AmenityCode.Gym,
        AmenityCode.Pool,
        AmenityCode.PetAllowed
    ];

    private static readonly string[] SampleRuleTexts =
    [
        "Không hút thuốc trong nhà",
        "Không nuôi thú cưng",
        "Giữ yên tĩnh sau 22 giờ",
        "Không tổ chức tiệc tùng"
    ];

    public static async Task<bool> SeedSampleAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Listings.AnyAsync(cancellationToken))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var listings = new List<Listing>(
            SamplePublishedCount + SampleDraftCount + SampleHiddenCount + 1);

        for (var index = 0; index < SamplePublishedCount; index++)
        {
            var listing = CreateSampleListing(index, ListingStatus.Published, now);
            AddSamplePhotos(listing, index);
            listings.Add(listing);
        }

        for (var index = 0; index < SampleDraftCount; index++)
        {
            listings.Add(CreateSampleListing(SamplePublishedCount + index, ListingStatus.Draft, now));
        }

        for (var index = 0; index < SampleHiddenCount; index++)
        {
            listings.Add(CreateSampleListing(
                SamplePublishedCount + SampleDraftCount + index,
                ListingStatus.Hidden,
                now));
        }

        listings.Add(CreateSampleListing(
            SamplePublishedCount + SampleDraftCount + SampleHiddenCount,
            ListingStatus.Removed,
            now));

        dbContext.Listings.AddRange(listings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<int> SeedBenchmarkAsync(
        AppDbContext dbContext,
        int count,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Listings.CountAsync(cancellationToken);
        if (existing >= count)
        {
            return 0;
        }

        var baseDate = DateTime.UtcNow;
        var toCreate = count - existing;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            for (var offset = 0; offset < toCreate; offset += BenchmarkBatchSize)
            {
                var batchCount = Math.Min(BenchmarkBatchSize, toCreate - offset);
                var listings = new List<Listing>(batchCount);

                for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
                {
                    var index = existing + offset + batchIndex;
                    listings.Add(CreateBenchmarkListing(index, baseDate));
                }

                dbContext.Listings.AddRange(listings);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return toCreate;
    }

    private static Listing CreateSampleListing(int index, ListingStatus status, DateTime now)
    {
        var location = Locations[index % Locations.Length];
        var bedrooms = index % 5;
        var titleTemplate = TitleTemplates[index % TitleTemplates.Length];
        var title = titleTemplate
            .Replace("{Bedrooms}", bedrooms == 0 ? "Studio" : bedrooms.ToString())
            .Replace("{District}", location.District);

        var createdAt = now
            .AddDays(-(index % 120))
            .AddHours(-(index % 24))
            .AddMinutes(-(index % 60));

        var listing = new Listing
        {
            Id = CreateDeterministicGuid($"sample-listing-{index}"),
            Title = title,
            Description =
                $"{title}. Diện tích {20 + (index * 7 % 90)} m², gần chợ, siêu thị và tuyến xe buýt. " +
                "Nhà mới sửa, nội thất cơ bản, phù hợp gia đình nhỏ hoặc người đi làm.",
            Price = 3_500_000L + (index % 40) * 1_150_000L,
            AreaM2 = 20 + (index * 7 % 90),
            Bedrooms = bedrooms,
            Address = $"{location.Address}, {location.District}, TP. Hồ Chí Minh",
            Lat = Math.Round(location.Lat + (index % 7 - 3) * 0.0015, 6),
            Lng = Math.Round(location.Lng + (index % 5 - 2) * 0.0015, 6),
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddHours(1)
        };

        AddSampleAmenities(listing, index);
        AddSampleRules(listing, index);

        return listing;
    }

    private static Listing CreateBenchmarkListing(int index, DateTime baseDate)
    {
        var location = Locations[index % Locations.Length];
        var bedrooms = index % 6;
        var title = $"Benchmark listing {index} - {bedrooms}PN {location.District}";
        var createdAt = baseDate.AddMinutes(-index);

        var listing = new Listing
        {
            Id = CreateDeterministicGuid($"benchmark-listing-{index}"),
            Title = title,
            Description = $"Benchmark row {index} for the listings performance test.",
            Price = 3_000_000L + index % 200 * 250_000L,
            AreaM2 = 18 + index % 110,
            Bedrooms = bedrooms,
            Address = $"{location.Address}, {location.District}, TP. Hồ Chí Minh",
            Lat = location.Lat,
            Lng = location.Lng,
            Status = ListingStatus.Published,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddMinutes(5)
        };

        listing.Photos.Add(new ListingPhoto
        {
            Id = CreateDeterministicGuid($"benchmark-listing-{index}-photo-0"),
            ListingId = listing.Id,
            Url = $"/media/listings/{listing.Id}/photo-0.jpg",
            ThumbUrl = $"/media/listings/{listing.Id}/thumb-0.jpg",
            Width = 1600,
            Height = 1067,
            Kind = ListingPhotoKind.Photo,
            Alt = title,
            SortOrder = 0,
            IsCover = true
        });

        return listing;
    }

    private static void AddSamplePhotos(Listing listing, int index)
    {
        if (index == 5)
        {
            return;
        }

        var photoCount = index == 10 ? 55 : 1 + index % 3;
        for (var photoIndex = 0; photoIndex < photoCount; photoIndex++)
        {
            var isPanorama = photoIndex is 2 or 3 && index == 10;
            listing.Photos.Add(new ListingPhoto
            {
                Id = CreateDeterministicGuid($"sample-listing-{index}-photo-{photoIndex}"),
                ListingId = listing.Id,
                Url = $"/media/listings/{listing.Id}/photo-{photoIndex}.jpg",
                ThumbUrl = $"/media/listings/{listing.Id}/thumb-{photoIndex}.jpg",
                Width = isPanorama ? 4096 : 1600,
                Height = isPanorama ? 2048 : 1067,
                Kind = isPanorama ? ListingPhotoKind.Panorama : ListingPhotoKind.Photo,
                Alt = $"{listing.Title} - ảnh {photoIndex + 1}",
                SortOrder = photoIndex,
                IsCover = photoIndex == 0
            });
        }
    }

    private static void AddSampleAmenities(Listing listing, int index)
    {
        if (index == NoDetailIndex)
        {
            return;
        }

        var amenityCount = 3 + index % 3;
        for (var amenityIndex = 0; amenityIndex < amenityCount; amenityIndex++)
        {
            var code = SampleAmenities[(index + amenityIndex) % SampleAmenities.Length];
            listing.Amenities.Add(new ListingAmenity
            {
                Id = CreateDeterministicGuid($"sample-listing-{index}-amenity-{code}"),
                ListingId = listing.Id,
                Code = code,
                SortOrder = amenityIndex
            });
        }
    }

    private static void AddSampleRules(Listing listing, int index)
    {
        if (index == NoDetailIndex)
        {
            return;
        }

        var ruleCount = 2 + index % 2;
        for (var ruleIndex = 0; ruleIndex < ruleCount; ruleIndex++)
        {
            listing.Rules.Add(new ListingRule
            {
                Id = CreateDeterministicGuid($"sample-listing-{index}-rule-{ruleIndex}"),
                ListingId = listing.Id,
                Text = SampleRuleTexts[(index + ruleIndex) % SampleRuleTexts.Length],
                SortOrder = ruleIndex
            });
        }
    }

    private static Guid CreateDeterministicGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }
}
