using System.Reflection;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>BR-MED-01..05, ERD §4.8: RoomPhoto invariants and factory/reconstitute behavior.</summary>
[TestFixture]
public class RoomPhotoTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

    private static RoomPhoto CreateValidPhoto(DateTime? utcNow = null, long sizeBytes = 1024) =>
        RoomPhoto.Create(
            uploadedByUserId: "landlord-1",
            kind: PhotoKind.Standard,
            contentType: "image/jpeg",
            sizeBytes: sizeBytes,
            width: 4000,
            height: 3000,
            originalPath: "rooms/2026/11/photo-1/original.jpg",
            mediumPath: "rooms/2026/11/photo-1/medium.jpg",
            thumbPath: "rooms/2026/11/photo-1/thumb.jpg",
            utcNow: utcNow ?? FixedNow);

    [Test]
    public void RoomPhoto_HasNoPublicParameterlessConstructor()
    {
        var publicParameterlessCtors = typeof(RoomPhoto)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length == 0)
            .ToList();

        Assert.That(publicParameterlessCtors, Is.Empty);
    }

    [Test]
    public void Create_WithValidData_StartsUnattachedAtSortOrderZero()
    {
        var photo = CreateValidPhoto();

        Assert.Multiple(() =>
        {
            Assert.That(photo.Id, Has.Length.EqualTo(32)); // Guid "N"
            Assert.That(photo.RoomId, Is.Null);            // BR-MED-03: unattached until the listing is saved
            Assert.That(photo.AttachedAtUtc, Is.Null);
            Assert.That(photo.SortOrder, Is.Zero);
            Assert.That(photo.UploadedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(photo.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(photo.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(photo.SizeBytes, Is.EqualTo(1024));
            Assert.That(photo.Width, Is.EqualTo(4000));
            Assert.That(photo.Height, Is.EqualTo(3000));
            Assert.That(photo.CreatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(photo.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Create_WithLocalTimestamp_StoresUtc()
    {
        var localNow = new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Local);

        var photo = CreateValidPhoto(localNow);

        Assert.Multiple(() =>
        {
            Assert.That(photo.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(photo.CreatedAtUtc, Is.EqualTo(localNow.ToUniversalTime()));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Create_WithBlankUploader_Throws(string? uploadedByUserId)
    {
        Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
            uploadedByUserId!, PhotoKind.Standard, "image/jpeg", 1024, 4000, 3000,
            "original.jpg", "medium.jpg", "thumb.jpg", FixedNow));
    }

    [Test]
    public void Create_WithUndefinedKind_Throws()
    {
        Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
            "landlord-1", (PhotoKind)99, "image/jpeg", 1024, 4000, 3000,
            "original.jpg", "medium.jpg", "thumb.jpg", FixedNow));
    }

    [TestCase("image/gif")]
    [TestCase("text/plain")]
    [TestCase("application/pdf")]
    [TestCase("IMAGE/JPEG")]
    [TestCase("")]
    public void Create_WithUnsupportedContentType_Throws(string contentType)
    {
        Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
            "landlord-1", PhotoKind.Standard, contentType, 1024, 4000, 3000,
            "original.jpg", "medium.jpg", "thumb.jpg", FixedNow));
    }

    [TestCase("image/png", TestName = "Create_WithPngContentType_IsAccepted")]
    [TestCase("image/jpeg", TestName = "Create_WithJpegContentType_IsAccepted")]
    public void Create_WithAllowedContentType_IsAccepted(string contentType)
    {
        var photo = RoomPhoto.Create(
            "landlord-1", PhotoKind.Standard, contentType, 1024, 4000, 3000,
            "original.jpg", "medium.jpg", "thumb.jpg", FixedNow);

        Assert.That(photo.ContentType, Is.EqualTo(contentType));
    }

    [TestCase(0L, TestName = "Create_WithZeroSize_Throws")]
    [TestCase(-1L, TestName = "Create_WithNegativeSize_Throws")]
    [TestCase(10_485_761L, TestName = "Create_WithSizeAbove10Mb_Throws")]
    public void Create_WithSizeOutOfRange_Throws(long sizeBytes)
    {
        Assert.Throws<DomainValidationException>(() => CreateValidPhoto(sizeBytes: sizeBytes));
    }

    [Test]
    public void Create_WithExactly10Mb_IsAccepted()
    {
        var photo = CreateValidPhoto(sizeBytes: 10_485_760);

        Assert.That(photo.SizeBytes, Is.EqualTo(10_485_760));
    }

    [TestCase(0, 3000, TestName = "Create_WithZeroWidth_Throws")]
    [TestCase(4000, 0, TestName = "Create_WithZeroHeight_Throws")]
    [TestCase(-1, 3000, TestName = "Create_WithNegativeWidth_Throws")]
    [TestCase(4000, -1, TestName = "Create_WithNegativeHeight_Throws")]
    public void Create_WithNonPositiveDimensions_Throws(int width, int height)
    {
        Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
            "landlord-1", PhotoKind.Standard, "image/jpeg", 1024, width, height,
            "original.jpg", "medium.jpg", "thumb.jpg", FixedNow));
    }

    [Test]
    public void Create_WithBlankPaths_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
                "landlord-1", PhotoKind.Standard, "image/jpeg", 1024, 4000, 3000,
                "", "medium.jpg", "thumb.jpg", FixedNow), "originalPath");
            Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
                "landlord-1", PhotoKind.Standard, "image/jpeg", 1024, 4000, 3000,
                "original.jpg", " ", "thumb.jpg", FixedNow), "mediumPath");
            Assert.Throws<DomainValidationException>(() => RoomPhoto.Create(
                "landlord-1", PhotoKind.Standard, "image/jpeg", 1024, 4000, 3000,
                "original.jpg", "medium.jpg", "", FixedNow), "thumbPath");
        });
    }

    [Test]
    public void Reconstitute_StoresEveryErdColumn()
    {
        var attachedAt = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

        var photo = RoomPhoto.Reconstitute(
            id: "photo-1",
            roomId: "room-1",
            uploadedByUserId: "landlord-1",
            kind: PhotoKind.Panorama,
            contentType: "image/png",
            sizeBytes: 2_048_000,
            width: 8000,
            height: 4000,
            originalPath: "rooms/2026/11/photo-1/original.png",
            mediumPath: "rooms/2026/11/photo-1/medium.png",
            thumbPath: "rooms/2026/11/photo-1/thumb.png",
            sortOrder: 4,
            createdAtUtc: FixedNow,
            attachedAtUtc: attachedAt);

        Assert.Multiple(() =>
        {
            Assert.That(photo.Id, Is.EqualTo("photo-1"));
            Assert.That(photo.RoomId, Is.EqualTo("room-1"));
            Assert.That(photo.UploadedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(photo.Kind, Is.EqualTo(PhotoKind.Panorama));
            Assert.That(photo.ContentType, Is.EqualTo("image/png"));
            Assert.That(photo.SizeBytes, Is.EqualTo(2_048_000));
            Assert.That(photo.Width, Is.EqualTo(8000));
            Assert.That(photo.Height, Is.EqualTo(4000));
            Assert.That(photo.OriginalPath, Is.EqualTo("rooms/2026/11/photo-1/original.png"));
            Assert.That(photo.MediumPath, Is.EqualTo("rooms/2026/11/photo-1/medium.png"));
            Assert.That(photo.ThumbPath, Is.EqualTo("rooms/2026/11/photo-1/thumb.png"));
            Assert.That(photo.SortOrder, Is.EqualTo(4));
            Assert.That(photo.CreatedAtUtc, Is.EqualTo(FixedNow));
            Assert.That(photo.AttachedAtUtc, Is.EqualTo(attachedAt));
            Assert.That(photo.AttachedAtUtc!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Reconstitute_WithNegativeSortOrder_Throws()
    {
        Assert.Throws<DomainValidationException>(() => RoomPhoto.Reconstitute(
            "photo-1", "room-1", "landlord-1", PhotoKind.Standard, "image/jpeg", 1024, 4000, 3000,
            "original.jpg", "medium.jpg", "thumb.jpg", sortOrder: -1, createdAtUtc: FixedNow));
    }
}
