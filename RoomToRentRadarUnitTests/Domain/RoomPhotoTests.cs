using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class RoomPhotoTests
{
    [Test]
    public void Constructor_WithValidInputs_InitializesProperties()
    {
        var createdAt = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Utc);

        var photo = CreatePhoto(createdAtUtc: createdAt);

        Assert.Multiple(() =>
        {
            Assert.That(photo.Id.Length, Is.EqualTo(32), "Id should be a 32-char N-format GUID.");
            Assert.That(Guid.TryParseExact(photo.Id, "N", out _), Is.True);
            Assert.That(photo.RoomId, Is.Null);
            Assert.That(photo.UploadedByUserId, Is.EqualTo("user-1"));
            Assert.That(photo.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(photo.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(photo.SizeBytes, Is.EqualTo(1024));
            Assert.That(photo.Width, Is.EqualTo(800));
            Assert.That(photo.Height, Is.EqualTo(600));
            Assert.That(photo.OriginalPath, Is.EqualTo("photos/photo-1/original.jpg"));
            Assert.That(photo.MediumPath, Is.EqualTo("photos/photo-1/medium.jpg"));
            Assert.That(photo.ThumbPath, Is.EqualTo("photos/photo-1/thumb.jpg"));
            Assert.That(photo.SortOrder, Is.Zero);
            Assert.That(photo.CreatedAtUtc, Is.EqualTo(createdAt));
            Assert.That(photo.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(photo.AttachedAtUtc, Is.Null);
        });
    }

    [Test]
    public void Constructor_WithUnspecifiedCreatedAt_NormalizesToUtc()
    {
        var unspecified = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Unspecified);

        var photo = CreatePhoto(createdAtUtc: unspecified);

        Assert.That(photo.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(photo.CreatedAtUtc, Is.EqualTo(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)));
    }

    [Test]
    public void Constructor_WithSizeBytesAtLimit_IsAllowed()
    {
        var photo = CreatePhoto(sizeBytes: PhotoFileRules.MaxFileBytes);

        Assert.That(photo.SizeBytes, Is.EqualTo(PhotoFileRules.MaxFileBytes));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithEmptyUploader_ThrowsDomainValidationException(string uploader)
    {
        Assert.That(() => CreatePhoto(uploadedByUserId: uploader), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithUndefinedKind_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(kind: (PhotoKind)99), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithUnsupportedContentType_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(contentType: "image/gif"), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithZeroSize_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(sizeBytes: 0), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithSizeAboveLimit_ThrowsDomainValidationException()
    {
        Assert.That(
            () => CreatePhoto(sizeBytes: PhotoFileRules.MaxFileBytes + 1),
            Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithZeroWidth_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(width: 0), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithNegativeHeight_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(height: -1), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithEmptyOriginalPath_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(originalPath: ""), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithEmptyMediumPath_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(mediumPath: "  "), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithEmptyThumbPath_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(thumbPath: ""), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithWhitespaceRoomId_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(roomId: " "), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithNegativeSortOrder_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(sortOrder: -1), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void Constructor_WithWhitespaceId_ThrowsDomainValidationException()
    {
        Assert.That(() => CreatePhoto(id: " "), Throws.TypeOf<DomainValidationException>());
    }

    private static RoomPhoto CreatePhoto(
        string uploadedByUserId = "user-1",
        PhotoKind kind = PhotoKind.Standard,
        string contentType = "image/jpeg",
        long sizeBytes = 1024,
        int width = 800,
        int height = 600,
        string originalPath = "photos/photo-1/original.jpg",
        string mediumPath = "photos/photo-1/medium.jpg",
        string thumbPath = "photos/photo-1/thumb.jpg",
        string? roomId = null,
        int sortOrder = 0,
        string? id = null,
        DateTime? createdAtUtc = null,
        DateTime? attachedAtUtc = null)
        => new(
            uploadedByUserId,
            kind,
            contentType,
            sizeBytes,
            width,
            height,
            originalPath,
            mediumPath,
            thumbPath,
            roomId,
            sortOrder,
            id,
            createdAtUtc,
            attachedAtUtc);
}
