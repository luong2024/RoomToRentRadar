using RoomToRentRadar.Constants;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class FavouriteRoomItemTests
{
    private static readonly Guid ListingId = Guid.Parse("6b0c9c1e-2f5a-4d8e-9a3b-1c7d5e2f8a44");

    [Test]
    public void FromDto_ShowsCurrentPriceAndAvailability()
    {
        var item = FavouriteRoomItem.FromDto(CreateFavourite(ListingStatus.Published));

        Assert.Multiple(() =>
        {
            Assert.That(item.PriceText, Is.EqualTo("1.500.000 đ"));
            Assert.That(item.AreaText, Is.EqualTo("25 m²"));
            Assert.That(item.BedroomsText, Is.EqualTo("1 PN"));
            Assert.That(item.StatusText, Is.EqualTo("Còn trống"));
            Assert.That(item.IsAvailable, Is.True);
        });
    }

    [Test]
    public void FromDto_WhenListingIsNoLongerPublished_MarksItUnavailable()
    {
        var item = FavouriteRoomItem.FromDto(CreateFavourite(ListingStatus.Hidden));

        Assert.Multiple(() =>
        {
            Assert.That(item.StatusText, Is.EqualTo("Tạm ẩn"));
            Assert.That(item.IsAvailable, Is.False);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void PhotoSource_WhenCoverPhotoMissing_UsesIllustration(string? coverPhotoUrl)
    {
        var item = FavouriteRoomItem.FromDto(CreateFavourite(ListingStatus.Published, coverPhotoUrl));

        Assert.That(item.PhotoSource, Is.EqualTo("room_1.png"));
    }

    [Test]
    public void PhotoSource_WhenCoverPhotoIsRelative_ResolvesAgainstApiHost()
    {
        var item = FavouriteRoomItem.FromDto(
            CreateFavourite(ListingStatus.Published, "/media/room.jpg"));

        Assert.That(
            item.PhotoSource,
            Is.EqualTo($"{ApiConstants.BaseAddress.TrimEnd('/')}/media/room.jpg"));
    }

    [TestCase("http://cdn.example.com/room.jpg")]
    [TestCase("https://cdn.example.com/room.jpg")]
    public void PhotoSource_WhenCoverPhotoIsAbsolute_KeepsUrl(string coverPhotoUrl)
    {
        var item = FavouriteRoomItem.FromDto(CreateFavourite(ListingStatus.Published, coverPhotoUrl));

        Assert.That(item.PhotoSource, Is.EqualTo(coverPhotoUrl));
    }

    private static FavouriteRoomDto CreateFavourite(ListingStatus status, string? coverPhotoUrl = null)
        => new(
            ListingId,
            "Sunny room",
            1_500_000,
            25,
            1,
            "1 Main St",
            status,
            coverPhotoUrl,
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
}
