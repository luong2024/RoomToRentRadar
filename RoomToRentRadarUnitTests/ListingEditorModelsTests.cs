using System.Globalization;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Models;
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

    [Test]
    public void OwnedListingItem_CoverThumbnailUrl_SelectsCoverPhotoAndResolvesUrl()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var nonCoverFirst = new ListingPhotoDto(
            Guid.NewGuid(), listingId, "/media/first.jpg", "/media/first-thumb.jpg",
            800, 600, ListingPhotoKind.Photo, null, SortOrder: 0, IsCover: false);
        var markedCoverSecond = new ListingPhotoDto(
            Guid.NewGuid(), listingId, "/media/cover.jpg", "/media/cover-thumb.jpg",
            800, 600, ListingPhotoKind.Photo, null, SortOrder: 1, IsCover: true);
        var listing = CreateListing(ListingStatus.Published, RoomType.Studio, 22, [nonCoverFirst, markedCoverSecond]);

        // Act
        var item = new OwnedListingItem(listing);

        // Assert
        var expectedUrl = new Uri(new Uri(ApiConstants.BaseAddress), "/media/cover-thumb.jpg").AbsoluteUri;
        Assert.Multiple(() =>
        {
            Assert.That(item.CoverThumbnailUrl, Is.EqualTo(expectedUrl));
            Assert.That(item.HasCoverThumbnail, Is.True);
            Assert.That(item.MissingCoverThumbnail, Is.False);
        });
    }

    [Test]
    public void OwnedListingItem_NoPhotos_ReturnsNullCoverThumbnailAndMarksMissing()
    {
        // Arrange
        var listing = CreateListing(ListingStatus.PendingReview, RoomType.SingleRoom, 18, []);

        // Act
        var item = new OwnedListingItem(listing);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(item.CoverThumbnailUrl, Is.Null);
            Assert.That(item.HasCoverThumbnail, Is.False);
            Assert.That(item.MissingCoverThumbnail, Is.True);
        });
    }

    [Test]
    public void OwnedListingItem_RoomSummaryText_FormatsVietnameseRoomTypeAndArea()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Arrange
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var listing = CreateListing(ListingStatus.Published, RoomType.SharedRoom, 20.5, []);
            var item = new OwnedListingItem(listing);

            // Act
            var summary = item.RoomSummaryText;

            // Assert
            Assert.That(summary, Is.EqualTo("Phòng ở ghép · 20.5 m²"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestCase(ListingStatus.PendingReview, true, true, false)]
    [TestCase(ListingStatus.Published, true, true, false)]
    [TestCase(ListingStatus.Hidden, true, false, true)]
    [TestCase(ListingStatus.Removed, false, false, false)]
    public void OwnedListingItem_ActionFlags_MatchListingStatus(
        ListingStatus status,
        bool expectedCanEdit,
        bool expectedCanUnpublish,
        bool expectedCanPublish)
    {
        // Arrange
        var listing = CreateListing(status, RoomType.Studio, 25, []);

        // Act
        var item = new OwnedListingItem(listing);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(item.Status, Is.EqualTo(status));
            Assert.That(item.StatusText, Is.EqualTo(ListingStatusText.For(status)));
            Assert.That(item.CanEdit, Is.EqualTo(expectedCanEdit));
            Assert.That(item.CanUnpublish, Is.EqualTo(expectedCanUnpublish));
            Assert.That(item.CanPublish, Is.EqualTo(expectedCanPublish));
        });
    }

    [Test]
    public void OwnedListingFilterOption_CountChanged_UpdatesDisplayTextAndRaisesPropertyChanged()
    {
        // Arrange
        var changedProperties = new List<string?>();
        var option = new OwnedListingFilterOption(ListingStatus.Published, "Đang đăng", _ => { });
        option.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        // Act
        option.Count = 4;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(option.DisplayText, Is.EqualTo("Đang đăng (4)"));
            Assert.That(changedProperties, Does.Contain(nameof(OwnedListingFilterOption.Count)));
            Assert.That(changedProperties, Does.Contain(nameof(OwnedListingFilterOption.DisplayText)));
        });
    }

    [Test]
    public void OwnedListingFilterOption_SelectCommand_InvokesCallbackWithConfiguredStatus()
    {
        // Arrange
        var selectedStatuses = new List<ListingStatus?>();
        var hiddenOption = new OwnedListingFilterOption(ListingStatus.Hidden, "Đã gỡ đăng", status => selectedStatuses.Add(status));
        var allOption = new OwnedListingFilterOption(null, "Tất cả", status => selectedStatuses.Add(status));

        // Act
        hiddenOption.SelectCommand.Execute(null);
        allOption.SelectCommand.Execute(null);

        // Assert
        Assert.That(selectedStatuses, Is.EqualTo(new ListingStatus?[] { ListingStatus.Hidden, null }));
    }

    private static ListingPhotoDto Photo(string? thumbnail)
        => new(Guid.NewGuid(), Guid.NewGuid(), "/media/room.jpg", thumbnail, 100, 100,
            ListingPhotoKind.Photo, null, 0, true);

    private static ManagedListingDto CreateListing(
        ListingStatus status,
        RoomType roomType,
        double areaM2,
        IReadOnlyList<ListingPhotoDto> photos)
    {
        var id = Guid.NewGuid();
        return new ManagedListingDto(
            id,
            "Phòng gần trường",
            "Yên tĩnh, thoáng mát",
            3_200_000,
            3_200_000,
            areaM2,
            roomType,
            1,
            "12 Đường Đại Học",
            10.76,
            106.68,
            status,
            new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc),
            [],
            [],
            "Chủ trọ",
            photos);
    }
}
