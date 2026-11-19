using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.Favourites.ViewModels;

namespace RoomToRentRadarUnitTests;

public class FavouriteListingItemTests
{
    private static FavouriteListingItem CreateItem(string? coverPhotoUrl)
        => new(
            Guid.NewGuid(),
            "Phòng gần trường",
            "3.000.000 đ",
            "20 m²",
            "● Còn trống",
            true,
            "Quận 1",
            "room_1.png")
        {
            CoverPhotoUrl = coverPhotoUrl
        };

    [Test]
    public void PhotoSource_ResolvesRelativeCoverUrlAgainstApiHost()
    {
        var item = CreateItem("/media/listings/abc/thumbs/photo.jpg");

        Assert.That(item.PhotoSource, Is.EqualTo(
            new Uri(new Uri(ApiConstants.BaseAddress), "/media/listings/abc/thumbs/photo.jpg").AbsoluteUri));
    }

    [Test]
    public void PhotoSource_KeepsAbsoluteCoverUrl()
    {
        var item = CreateItem("https://cdn.example.com/room.jpg");

        Assert.That(item.PhotoSource, Is.EqualTo("https://cdn.example.com/room.jpg"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void PhotoSource_FallsBackToIllustrationWithoutCoverUrl(string? coverPhotoUrl)
    {
        var item = CreateItem(coverPhotoUrl);

        Assert.That(item.PhotoSource, Is.EqualTo("room_1.png"));
    }

    [Test]
    public void PhotoSource_FallsBackToIllustrationForUnsupportedScheme()
    {
        var item = CreateItem("ftp://example.com/room.jpg");

        Assert.That(item.PhotoSource, Is.EqualTo("room_1.png"));
    }
}
