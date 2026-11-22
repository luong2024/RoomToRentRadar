using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Tests;

[TestFixture]
public class RoomPhotoTests
{
    [Test]
    public void Create_WithValidParameters_ReturnsConfiguredRoomPhoto()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var photo = RoomPhoto.Create(
            id: "photo-1",
            roomId: "room-ictu-001",
            url: "https://cdn.example.com/rooms/room-ictu-001/photo-1.jpg",
            thumbnailUrl: "https://cdn.example.com/rooms/room-ictu-001/photo-1_thumb.jpg",
            objectKey: "rooms/room-ictu-001/photo-1.jpg",
            thumbnailObjectKey: "rooms/room-ictu-001/photo-1_thumb.jpg",
            contentType: "IMAGE/JPEG",
            sizeBytes: 245_000,
            width: 1920,
            height: 1080,
            is360: true,
            isCover: true,
            displayOrder: 0,
            caption: "  Main room 360 view  ",
            uploadedByUserId: userId,
            uploadedAtUtc: now);

        Assert.Multiple(() =>
        {
            Assert.That(photo.Id, Is.EqualTo("photo-1"));
            Assert.That(photo.RoomId, Is.EqualTo("room-ictu-001"));
            Assert.That(photo.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(photo.Is360, Is.True);
            Assert.That(photo.IsCover, Is.True);
            Assert.That(photo.Caption, Is.EqualTo("Main room 360 view"));
            Assert.That(photo.UploadedByUserId, Is.EqualTo(userId));
            Assert.That(RoomPhoto.MaxPhotosPerRoom, Is.GreaterThanOrEqualTo(8));
        });
    }

    [Test]
    public void Create_WhenSizeExceeds10Mb_ThrowsInvalidRoomPhotoException()
    {
        Assert.Throws<InvalidRoomPhotoException>(() => RoomPhoto.Create(
            id: "photo-1",
            roomId: "room-001",
            url: "https://cdn.example.com/p.jpg",
            thumbnailUrl: "https://cdn.example.com/p_thumb.jpg",
            objectKey: "rooms/room-001/p.jpg",
            thumbnailObjectKey: "rooms/room-001/p_thumb.jpg",
            contentType: "image/jpeg",
            sizeBytes: RoomPhoto.MaxFileSizeBytes + 1,
            width: 1920,
            height: 1080,
            is360: false,
            isCover: false,
            displayOrder: 0,
            caption: null,
            uploadedByUserId: Guid.NewGuid(),
            uploadedAtUtc: DateTimeOffset.UtcNow));
    }

    [Test]
    public void Create_WhenCaptionExceedsMaxLength_ThrowsInvalidRoomPhotoException()
    {
        var longCaption = new string('a', RoomPhoto.MaxCaptionLength + 1);

        Assert.Throws<InvalidRoomPhotoException>(() => RoomPhoto.Create(
            id: "photo-1",
            roomId: "room-001",
            url: "https://cdn.example.com/p.jpg",
            thumbnailUrl: "https://cdn.example.com/p_thumb.jpg",
            objectKey: "rooms/room-001/p.jpg",
            thumbnailObjectKey: "rooms/room-001/p_thumb.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024,
            width: 800,
            height: 600,
            is360: false,
            isCover: false,
            displayOrder: 0,
            caption: longCaption,
            uploadedByUserId: Guid.NewGuid(),
            uploadedAtUtc: DateTimeOffset.UtcNow));
    }

    [Test]
    public void SetDisplayOrder_WhenNegative_ThrowsInvalidRoomPhotoException()
    {
        var photo = RoomPhoto.Create(
            id: "photo-1",
            roomId: "room-001",
            url: "https://cdn.example.com/p.jpg",
            thumbnailUrl: "https://cdn.example.com/p_thumb.jpg",
            objectKey: "rooms/room-001/p.jpg",
            thumbnailObjectKey: "rooms/room-001/p_thumb.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024,
            width: 800,
            height: 600,
            is360: false,
            isCover: false,
            displayOrder: 0,
            caption: null,
            uploadedByUserId: Guid.NewGuid(),
            uploadedAtUtc: DateTimeOffset.UtcNow);

        Assert.Throws<InvalidRoomPhotoException>(() => photo.SetDisplayOrder(-1));
    }
}
