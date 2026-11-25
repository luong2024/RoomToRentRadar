using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar.Api.Seed;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Photos;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.SqliteDataAccess.Seed;
using SkiaSharp;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class SeedMediaWriterTests
{
    private static readonly Guid ListingId = new("00000000-0000-0000-0000-000000000042");

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private string _contentRoot = null!;
    private string _mediaRoot = null!;
    private SeedMediaWriter _writer = null!;

    [SetUp]
    public void SetUp()
    {
        _contentRoot = Path.Combine(
            Path.GetTempPath(),
            "RoomToRentRadarSeedMediaTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);

        var options = PhotoStorageOptions.Resolve(null, _contentRoot);
        _mediaRoot = options.RootPath;
        _writer = new SeedMediaWriter(options, NullLogger<SeedMediaWriter>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Test]
    public void EnsureFiles_WritesPhotoThumbnailAndPanoramaFiles()
    {
        var written = _writer.EnsureFiles(CreatePhotos());

        Assert.That(written, Is.EqualTo(4));
        foreach (var path in AllPaths())
        {
            Assert.That(File.Exists(path), Is.True, path);
            var content = File.ReadAllBytes(path);
            Assert.Multiple(() =>
            {
                Assert.That(content, Is.Not.Empty, path);
                Assert.That(content.Take(JpegSignature.Length), Is.EqualTo(JpegSignature), path);
            });
        }
    }

    [Test]
    public void EnsureFiles_WhenFilesExist_WritesNothing()
    {
        _writer.EnsureFiles(CreatePhotos());

        var written = _writer.EnsureFiles(CreatePhotos());

        Assert.That(written, Is.Zero);
    }

    [Test]
    public void EnsureFiles_ProducesExpectedDimensions()
    {
        _writer.EnsureFiles(CreatePhotos());

        Assert.Multiple(() =>
        {
            Assert.That(ReadDimensions(PhotoPath("photo-0.jpg")), Is.EqualTo((1600, 1067)));
            Assert.That(ReadDimensions(PhotoPath("thumb-0.jpg")), Is.EqualTo((480, 320)));
            Assert.That(ReadDimensions(PhotoPath("photo-1.jpg")), Is.EqualTo((2048, 1024)));
            Assert.That(ReadDimensions(PhotoPath("thumb-1.jpg")), Is.EqualTo((480, 240)));
        });
    }

    [Test]
    public async Task EnsureSampleMediaAsync_WritesOnlySampleListingPhotos()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_contentRoot, "seed-media.db")}")
            .Options;
        await using var dbContext = new AppDbContext(dbOptions);
        await dbContext.Database.MigrateAsync();

        var sampleListingId = DatabaseSeeder.GetSampleListingIds()[0];
        var otherListingId = Guid.NewGuid();
        AddListing(dbContext, sampleListingId);
        AddListing(dbContext, otherListingId);
        await dbContext.SaveChangesAsync();

        var written = await _writer.EnsureSampleMediaAsync(dbContext);

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(2));
            Assert.That(
                File.Exists(Path.Combine(_mediaRoot, "listings", sampleListingId.ToString("D"), "photo-0.jpg")),
                Is.True);
            Assert.That(
                File.Exists(Path.Combine(_mediaRoot, "listings", sampleListingId.ToString("D"), "thumb-0.jpg")),
                Is.True);
            Assert.That(
                Directory.Exists(Path.Combine(_mediaRoot, "listings", otherListingId.ToString("D"))),
                Is.False);
        });
    }

    private static void AddListing(AppDbContext dbContext, Guid listingId)
    {
        dbContext.Listings.Add(new Listing
        {
            Id = listingId,
            Title = "Seed media listing",
            Description = "Seed media listing description",
            Price = 5_000_000L,
            Deposit = 5_000_000L,
            AreaM2 = 30,
            Address = "123 Test Street",
            Lat = 10.7769,
            Lng = 106.7009,
            Status = ListingStatus.Published,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LandlordName = "Seed Landlord",
            LandlordPhone = "0900 000 000",
            Photos =
            {
                new ListingPhoto
                {
                    Id = Guid.NewGuid(),
                    ListingId = listingId,
                    Url = $"/media/listings/{listingId}/photo-0.jpg",
                    ThumbUrl = $"/media/listings/{listingId}/thumb-0.jpg",
                    Width = 1600,
                    Height = 1067,
                    Kind = ListingPhotoKind.Photo,
                    SortOrder = 0,
                    IsCover = true
                }
            }
        });
    }

    private static IReadOnlyList<ListingPhoto> CreatePhotos() =>
    [
        new ListingPhoto
        {
            Id = new Guid("11111111-0000-0000-0000-000000000000"),
            ListingId = ListingId,
            Url = $"/media/listings/{ListingId}/photo-0.jpg",
            ThumbUrl = $"/media/listings/{ListingId}/thumb-0.jpg",
            Width = 1600,
            Height = 1067,
            Kind = ListingPhotoKind.Photo,
            SortOrder = 0,
            IsCover = true
        },
        new ListingPhoto
        {
            Id = new Guid("11111111-0000-0000-0000-000000000001"),
            ListingId = ListingId,
            Url = $"/media/listings/{ListingId}/photo-1.jpg",
            ThumbUrl = $"/media/listings/{ListingId}/thumb-1.jpg",
            Width = 2048,
            Height = 1024,
            Kind = ListingPhotoKind.Panorama,
            SortOrder = 1,
            IsCover = false
        }
    ];

    private string[] AllPaths() =>
        new[] { "photo-0.jpg", "thumb-0.jpg", "photo-1.jpg", "thumb-1.jpg" }
            .Select(PhotoPath)
            .ToArray();

    private string PhotoPath(string fileName) =>
        Path.Combine(_mediaRoot, "listings", ListingId.ToString("D"), fileName);

    private static (int Width, int Height) ReadDimensions(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        Assert.That(bitmap, Is.Not.Null, path);
        return (bitmap!.Width, bitmap.Height);
    }
}
