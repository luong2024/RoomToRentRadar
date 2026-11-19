using System.Net;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;
using ListingSubmission = RoomToRentRadar.Models.ListingSubmission;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MyListingsViewModelTests
{
    [Test]
    public async Task LoadAsync_WhenApiReturnsListings_PopulatesCardsWithStatuses()
    {
        var published = CreateListing(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ListingStatus.Published,
            "Phòng đã đăng");
        var pending = CreateListing(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ListingStatus.PendingReview,
            "Phòng chờ duyệt");
        var apiClient = new FakeApiClient { Listings = [published, pending] };
        var viewModel = new MyListingsViewModel(apiClient, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(2));
            Assert.That(viewModel.IsEmpty, Is.False);
            Assert.That(viewModel.Message, Is.Empty);
            Assert.That(viewModel.ErrorMessage, Is.Empty);
            Assert.That(viewModel.IsSignInRequired, Is.False);

            var first = viewModel.Listings[0];
            Assert.That(first.Title, Is.EqualTo("Phòng đã đăng"));
            Assert.That(first.PriceText, Is.EqualTo("2.500.000 đ"));
            Assert.That(first.AreaText, Is.EqualTo("20 m²"));
            Assert.That(first.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(first.StatusText, Is.EqualTo("Đã đăng"));
            Assert.That(first.CanUnpublish, Is.True);
            Assert.That(first.IsActionEnabled, Is.True);
            Assert.That(first.ShowIllustrationNote, Is.True);

            Assert.That(viewModel.Listings[1].StatusText, Is.EqualTo("Chờ duyệt"));
        });
    }

    [Test]
    public async Task LoadAsync_WhenApiReturnsNothing_ShowsTheEmptyMessage()
    {
        var viewModel = new MyListingsViewModel(new FakeApiClient(), new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.IsEmpty, Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Bạn chưa có tin đăng nào."));
            Assert.That(viewModel.HasError, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WhenUnauthorized_AsksTheLandlordToSignInAgain()
    {
        var apiClient = new FakeApiClient
        {
            ListingsException = new ApiException(HttpStatusCode.Unauthorized, "expired")
        };
        var viewModel = new MyListingsViewModel(apiClient, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignInRequired, Is.True);
            Assert.That(viewModel.Message,
                Is.EqualTo("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
            Assert.That(viewModel.Listings, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenTheApiFails_ShowsTheRetryMessage()
    {
        var apiClient = new FakeApiClient
        {
            ListingsException = new HttpRequestException("network down")
        };
        var viewModel = new MyListingsViewModel(apiClient, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEmpty, Is.True);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage,
                Is.EqualTo("Không tải được danh sách tin đăng. Vui lòng thử lại."));
            Assert.That(viewModel.IsSignInRequired, Is.False);
        });
    }

    [Test]
    public async Task UnpublishAsync_WhenItSucceeds_UpdatesTheCardAndConfirms()
    {
        var listing = CreateListing(Guid.NewGuid(), ListingStatus.Published, "Phòng đẹp");
        var hidden = listing with { Status = ListingStatus.Hidden };
        var apiClient = new FakeApiClient
        {
            Listings = [listing],
            UnpublishResult = hidden
        };
        var viewModel = new MyListingsViewModel(apiClient, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);
        var item = viewModel.Listings[0];

        await viewModel.UnpublishCommand.ExecuteAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.UnpublishRequestedIds, Is.EqualTo(new[] { listing.Id }));
            Assert.That(item.Status, Is.EqualTo(ListingStatus.Hidden));
            Assert.That(item.StatusText, Is.EqualTo("Đã ẩn"));
            Assert.That(item.CanUnpublish, Is.False);
            Assert.That(viewModel.OperationMessage, Is.EqualTo("Đã ẩn tin “Phòng đẹp”."));
            Assert.That(viewModel.HasError, Is.False);
        });
    }

    [Test]
    public async Task UnpublishAsync_WhenTheListingIsGone_ReloadsTheList()
    {
        var listing = CreateListing(Guid.NewGuid(), ListingStatus.Published);
        var apiClient = new FakeApiClient
        {
            Listings = [listing],
            UnpublishException = new ApiException(HttpStatusCode.NotFound, "gone", "listing_not_found")
        };
        var viewModel = new MyListingsViewModel(apiClient, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);
        var item = viewModel.Listings[0];

        // The listing disappeared on the server, so the reload returns an empty list.
        apiClient.Listings = [];

        await viewModel.UnpublishCommand.ExecuteAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.GetMyListingsCallCount, Is.EqualTo(2));
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.IsEmpty, Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Bạn chưa có tin đăng nào."));
        });
    }

    [Test]
    public async Task UnpublishAsync_WhenUnauthorized_AsksTheLandlordToSignInAgain()
    {
        var listing = CreateListing(Guid.NewGuid(), ListingStatus.Published);
        var apiClient = new FakeApiClient
        {
            Listings = [listing],
            UnpublishException = new ApiException(HttpStatusCode.Unauthorized, "expired")
        };
        var viewModel = new MyListingsViewModel(apiClient, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.UnpublishCommand.ExecuteAsync(viewModel.Listings[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignInRequired, Is.True);
            Assert.That(viewModel.ErrorMessage,
                Is.EqualTo("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
        });
    }

    [Test]
    public async Task CreateAsync_OpensTheEditorWithoutId()
    {
        var navigation = new FakeNavigationService();
        var viewModel = new MyListingsViewModel(new FakeApiClient(), navigation);

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.That(navigation.EditorRequests, Is.EqualTo(new Guid?[] { null }));
    }

    [Test]
    public async Task EditAsync_OpensTheEditorForTheTappedListing()
    {
        var listing = CreateListing(Guid.NewGuid(), ListingStatus.Draft);
        var apiClient = new FakeApiClient { Listings = [listing] };
        var navigation = new FakeNavigationService();
        var viewModel = new MyListingsViewModel(apiClient, navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.EditCommand.ExecuteAsync(viewModel.Listings[0]);

        Assert.That(navigation.EditorRequests, Is.EqualTo(new Guid?[] { listing.Id }));
    }

    [Test]
    public async Task SignInAsync_OpensTheLoginPage()
    {
        var navigation = new FakeNavigationService();
        var viewModel = new MyListingsViewModel(new FakeApiClient(), navigation);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigation.LoginRequests, Is.EqualTo(1));
    }

    [TestCase(ListingStatus.Draft, "Nháp", false)]
    [TestCase(ListingStatus.PendingReview, "Chờ duyệt", true)]
    [TestCase(ListingStatus.Published, "Đã đăng", true)]
    [TestCase(ListingStatus.Hidden, "Đã ẩn", false)]
    [TestCase(ListingStatus.Removed, "Đã gỡ", false)]
    public void MyListingsItem_DescribesTheStatusAndWhenItCanBeHidden(
        ListingStatus status,
        string expectedText,
        bool canUnpublish)
    {
        var item = new MyListingsItem(CreateListing(Guid.NewGuid(), status));

        Assert.Multiple(() =>
        {
            Assert.That(item.StatusText, Is.EqualTo(expectedText));
            Assert.That(item.CanUnpublish, Is.EqualTo(canUnpublish));
        });
    }

    [Test]
    public void MyListingsItem_UsesTheCoverPhotoWhenTheListingHasPhotos()
    {
        var listing = CreateListing(Guid.NewGuid(), ListingStatus.Published) with
        {
            Photos =
            [
                new ListingPhotoDto(
                    Guid.NewGuid(),
                    Guid.Empty,
                    "/uploads/phong-1.jpg",
                    "/uploads/phong-1-thumb.jpg",
                    800,
                    600,
                    ListingPhotoKind.Photo,
                    null,
                    0,
                    IsCover: true)
            ]
        };

        var item = new MyListingsItem(listing);

        Assert.Multiple(() =>
        {
            Assert.That(item.HasCoverPhoto, Is.True);
            Assert.That(item.ShowIllustrationNote, Is.False);
            Assert.That(item.PhotoSource, Does.EndWith("/uploads/phong-1-thumb.jpg"));
        });
    }

    private static ManagedListingDto CreateListing(
        Guid id,
        ListingStatus status,
        string title = "Phòng trọ giá rẻ",
        long price = 2_500_000,
        double areaM2 = 20)
        => new(
            id,
            title,
            "Mô tả phòng",
            price,
            price,
            areaM2,
            RoomType.Studio,
            1,
            "12 Nguyễn Văn Cừ, Quận 5",
            10.7721,
            106.6579,
            status,
            new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
            [],
            [],
            "Chủ trọ",
            []);

    private sealed class FakeNavigationService : INavigationService
    {
        public List<Guid?> EditorRequests { get; } = new();

        public int LoginRequests { get; private set; }

        public int BackRequests { get; private set; }

        public Task GoToRoomDetailAsync(Guid listingId) => Task.CompletedTask;

        public Task GoToLoginAsync()
        {
            LoginRequests++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null)
        {
            EditorRequests.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("My listings does not open directions.");

        public Task GoBackAsync()
        {
            BackRequests++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApiClient : IApiClient
    {
        public IReadOnlyList<ManagedListingDto> Listings { get; set; } = [];

        public Exception? ListingsException { get; set; }

        public ManagedListingDto? UnpublishResult { get; set; }

        public Exception? UnpublishException { get; set; }

        public int GetMyListingsCallCount { get; private set; }

        public List<Guid> UnpublishRequestedIds { get; } = new();

        public Task<IReadOnlyList<ManagedListingDto>> GetMyListingsAsync(
            CancellationToken cancellationToken = default)
        {
            GetMyListingsCallCount++;
            return ListingsException is not null
                ? Task.FromException<IReadOnlyList<ManagedListingDto>>(ListingsException)
                : Task.FromResult(Listings);
        }

        public Task<ManagedListingDto> UnpublishListingAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            UnpublishRequestedIds.Add(id);
            return UnpublishException is not null
                ? Task.FromException<ManagedListingDto>(UnpublishException)
                : Task.FromResult(UnpublishResult!);
        }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingDetailFetchResult> GetListingDetailAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LoginResponse?> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ManagedListingDto> GetManagedListingAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ManagedListingDto> CreateListingAsync(
            ListingSubmission submission,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ManagedListingDto> UpdateListingAsync(
            Guid id,
            ListingSubmission submission,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
