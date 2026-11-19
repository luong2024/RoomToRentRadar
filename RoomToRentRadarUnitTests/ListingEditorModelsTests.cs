using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingEditorModelsTests
{
    [Test]
    public void ExistingPhoto_RelativeThumbnail_ResolvesAgainstApiBaseAddress()
    {
        var photo = new ExistingListingPhoto(Photo("/media/room-thumb.jpg"));

        Assert.That(photo.ThumbnailUrl, Is.EqualTo(new Uri(new Uri(ApiConstants.BaseAddress), "/media/room-thumb.jpg").AbsoluteUri));
    }

    [Test]
    public void ExistingPhoto_AbsoluteThumbnail_PreservesItsAddress()
    {
        var photo = new ExistingListingPhoto(Photo("https://images.example.test/room.jpg"));

        Assert.That(photo.ThumbnailUrl, Is.EqualTo("https://images.example.test/room.jpg"));
    }

    [Test]
    public void ExistingPhoto_MissingThumbnail_UsesOriginalPhoto()
    {
        var photo = new ExistingListingPhoto(Photo(null));

        Assert.That(photo.ThumbnailUrl, Is.EqualTo(new Uri(new Uri(ApiConstants.BaseAddress), "/media/room.jpg").AbsoluteUri));
    }

    private static ListingPhotoDto Photo(string? thumbnail)
        => new(Guid.NewGuid(), Guid.NewGuid(), "/media/room.jpg", thumbnail, 100, 100,
            ListingPhotoKind.Photo, null, 0, true);
}
