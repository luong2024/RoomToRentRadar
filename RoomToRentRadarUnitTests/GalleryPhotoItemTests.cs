using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class GalleryPhotoItemTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");
    private static readonly Guid PhotoId = Guid.Parse("0b0f5f18-1e73-4f63-9a1c-2c4f0b7d0001");

    [Test]
    public void Constructor_WhenDtoProvided_CopiesTheIdentityDimensionsAndFlags()
    {
        var item = CreateItem(width: 4000, height: 1000, isCover: true);

        Assert.Multiple(() =>
        {
            Assert.That(item.Id, Is.EqualTo(PhotoId));
            Assert.That(item.Width, Is.EqualTo(4000));
            Assert.That(item.Height, Is.EqualTo(1000));
            Assert.That(item.IsCover, Is.True);
        });
    }

    [Test]
    public void FullUrl_WhenTheDtoUrlIsRelative_ResolvesAgainstTheApiBaseAddress()
    {
        var withLeadingSlash = CreateItem(url: "/media/room-1.jpg");
        var withoutLeadingSlash = CreateItem(url: "media/room-1.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(withLeadingSlash.FullUrl, Is.EqualTo("http://localhost:5080/media/room-1.jpg"));
            Assert.That(withoutLeadingSlash.FullUrl, Is.EqualTo("http://localhost:5080/media/room-1.jpg"));
            Assert.That(new Uri(withLeadingSlash.FullUrl).IsAbsoluteUri, Is.True);
        });
    }

    [Test]
    public void FullUrl_WhenTheDtoUrlIsAbsolute_IsLeftUnchanged()
    {
        var item = CreateItem(url: "https://cdn.example.com/rooms/room-1.jpg");

        Assert.That(item.FullUrl, Is.EqualTo("https://cdn.example.com/rooms/room-1.jpg"));
    }

    [TestCase(null, TestName = "ThumbUrl_WhenTheDtoThumbIsNull_FallsBackToTheFullUrl")]
    [TestCase("", TestName = "ThumbUrl_WhenTheDtoThumbIsEmpty_FallsBackToTheFullUrl")]
    [TestCase("   ", TestName = "ThumbUrl_WhenTheDtoThumbIsWhitespace_FallsBackToTheFullUrl")]
    public void ThumbUrl_WhenTheDtoThumbIsBlank_FallsBackToTheFullUrl(string? thumbUrl)
    {
        var item = CreateItem(url: "/media/room-1.jpg", thumbUrl: thumbUrl);

        Assert.Multiple(() =>
        {
            Assert.That(item.FullUrl, Is.EqualTo("http://localhost:5080/media/room-1.jpg"));
            Assert.That(item.ThumbUrl, Is.EqualTo(item.FullUrl));
        });
    }

    [Test]
    public void ThumbUrl_WhenTheDtoThumbIsProvided_ResolvesAgainstTheApiBaseAddress()
    {
        var item = CreateItem(url: "/media/room-1.jpg", thumbUrl: "/media/room-1-thumb.jpg");

        Assert.That(item.ThumbUrl, Is.EqualTo("http://localhost:5080/media/room-1-thumb.jpg"));
    }

    [Test]
    public void IsPanorama_IsTrueOnlyForThePanoramaKind()
    {
        var photo = CreateItem(kind: ListingPhotoKind.Photo);
        var panorama = CreateItem(kind: ListingPhotoKind.Panorama);

        Assert.Multiple(() =>
        {
            Assert.That(photo.IsPanorama, Is.False);
            Assert.That(panorama.IsPanorama, Is.True);
            Assert.That(panorama.Kind, Is.EqualTo(ListingPhotoKind.Panorama));
        });
    }

    [TestCase(null, TestName = "AccessibleDescription_WhenTheAltIsNull_UsesTheDefault")]
    [TestCase("", TestName = "AccessibleDescription_WhenTheAltIsEmpty_UsesTheDefault")]
    [TestCase("   ", TestName = "AccessibleDescription_WhenTheAltIsWhitespace_UsesTheDefault")]
    public void AccessibleDescription_WhenTheAltIsBlank_UsesTheDefault(string? alt)
    {
        var item = CreateItem(alt: alt);

        Assert.That(item.AccessibleDescription, Is.EqualTo("Ảnh phòng"));
    }

    [Test]
    public void AccessibleDescription_WhenTheAltIsProvided_UsesTheAltText()
    {
        var item = CreateItem(alt: "Ban cong huong dong");

        Assert.That(item.AccessibleDescription, Is.EqualTo("Ban cong huong dong"));
    }

    [Test]
    public void AspectRatio_WhenTheHeightIsMissing_FallsBackToFourThirds()
    {
        var item = CreateItem(width: 1200, height: 0);

        Assert.That(item.AspectRatio, Is.EqualTo(4d / 3d).Within(1e-9));
    }

    [Test]
    public void AspectRatio_WhenTheHeightIsPositive_DividesTheWidthByTheHeight()
    {
        var item = CreateItem(width: 1600, height: 900);

        Assert.That(item.AspectRatio, Is.EqualTo(1600d / 900d).Within(1e-9));
    }

    [Test]
    public void FullSourceAndThumbSource_WhenAccessedTwice_ReturnTheSameInstance()
    {
        var item = CreateItem(url: "/media/room-1.jpg", thumbUrl: "/media/room-1-thumb.jpg");

        var fullSource = item.FullSource;
        var thumbSource = item.ThumbSource;

        Assert.Multiple(() =>
        {
            Assert.That(item.FullSource, Is.SameAs(fullSource));
            Assert.That(item.ThumbSource, Is.SameAs(thumbSource));
        });
    }

    private static GalleryPhotoItem CreateItem(
        string url = "/media/room-1.jpg",
        string? thumbUrl = null,
        int width = 1600,
        int height = 900,
        ListingPhotoKind kind = ListingPhotoKind.Photo,
        string? alt = null,
        bool isCover = false)
        => new(
            new ListingPhotoDto(
                PhotoId,
                ListingId,
                url,
                thumbUrl,
                width,
                height,
                kind,
                alt,
                SortOrder: 0,
                IsCover: isCover));
}
