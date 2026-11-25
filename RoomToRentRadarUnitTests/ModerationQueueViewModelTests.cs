using System.Globalization;
using System.Net;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ModerationQueueViewModelTests
{
    [Test]
    public async Task Load_AsModerator_ShowsPendingListings()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page("next-cursor", hasMore: true, Listing("Phòng A"), Listing("Phòng B")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Moderator));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Content));
            Assert.That(viewModel.IsContentState, Is.True);
            Assert.That(viewModel.Items.Select(item => item.Title), Is.EqualTo(new[] { "Phòng A", "Phòng B" }));
            Assert.That(viewModel.HasMore, Is.True);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(client.Requests, Has.Count.EqualTo(1));
            Assert.That(client.Requests[0].Limit, Is.EqualTo(20));
            Assert.That(client.Requests[0].Cursor, Is.Null);
        });
    }

    [Test]
    public async Task Load_AsAdmin_ShowsPendingListings()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Content));
            Assert.That(viewModel.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Load_AsSignedOut_ShowsSignedOutWithoutCallingApi()
    {
        var client = new FakeModerationClient();
        var viewModel = CreateViewModel(client, new FakeTokenStore());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.SignedOut));
            Assert.That(viewModel.IsSignedOutState, Is.True);
            Assert.That(client.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Load_AsStudent_ShowsDeniedWithoutCallingApi()
    {
        var client = new FakeModerationClient();
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Student));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Denied));
            Assert.That(viewModel.IsDeniedState, Is.True);
            Assert.That(client.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Load_WhenApiForbids_ShowsDenied()
    {
        var client = new FakeModerationClient
        {
            ListException = new ApiException(HttpStatusCode.Forbidden, "Forbidden.", "forbidden")
        };
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Moderator));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Denied));
    }

    [Test]
    public async Task Load_WhenApiRejectsToken_ShowsSignedOut()
    {
        var client = new FakeModerationClient
        {
            ListException = new ApiException(HttpStatusCode.Unauthorized, "Unauthorized.", "unauthorized")
        };
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.SignedOut));
    }

    [Test]
    public async Task Load_WhenApiFails_ShowsErrorStateWithMessage()
    {
        var client = new FakeModerationClient { ListException = new HttpRequestException("offline") };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            client,
            SignedInAs(AppUserRole.Moderator),
            errorHandler: errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Error));
            Assert.That(viewModel.IsErrorState, Is.True);
            Assert.That(viewModel.HasMessage, Is.True);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Load_WhenRoleLookupFails_ShowsErrorState()
    {
        var client = new FakeModerationClient();
        var tokenStore = new FakeTokenStore
        {
            AccessToken = "test-token",
            RoleException = new InvalidOperationException("secure storage unavailable")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(client, tokenStore, errorHandler: errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Error));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(client.Requests, Is.Empty);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Load_WhenQueueIsEmpty_ShowsEmptyState()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Moderator));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Empty));
            Assert.That(viewModel.IsEmptyState, Is.True);
            Assert.That(viewModel.Items, Is.Empty);
        });
    }

    [Test]
    public async Task LoadMore_AppendsTheNextPage()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page("cursor-2", hasMore: true, Listing("Phòng A")));
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng B")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Moderator));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.Title), Is.EqualTo(new[] { "Phòng A", "Phòng B" }));
            Assert.That(viewModel.HasMore, Is.False);
            Assert.That(client.Requests, Has.Count.EqualTo(2));
            Assert.That(client.Requests[0].Cursor, Is.Null);
            Assert.That(client.Requests[1].Cursor, Is.EqualTo("cursor-2"));
        });
    }

    [Test]
    public async Task LoadMore_WithoutMorePages_DoesNotCallApi()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Moderator));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        Assert.That(client.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Approve_WhenConfirmed_RemovesListingAndShowsConfirmation()
    {
        var listing = Listing("Phòng A");
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, listing));
        var prompt = new FakeAlertPrompt();
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin), prompt);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.ConfirmCalls, Is.EqualTo(1));
            Assert.That(client.Approved, Is.EqualTo(new[] { listing.Id }));
            Assert.That(viewModel.Items, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Empty));
            Assert.That(viewModel.Message, Does.StartWith("Đã duyệt tin"));
            Assert.That(viewModel.Message, Does.Contain("Phòng A"));
        });
    }

    [Test]
    public async Task Approve_WhenCancelled_DoesNotCallApi()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var prompt = new FakeAlertPrompt { ConfirmResult = false };
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin), prompt);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(client.Approved, Is.Empty);
            Assert.That(viewModel.Items, Has.Count.EqualTo(1));
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Content));
        });
    }

    [Test]
    public async Task Approve_WhenListingWasAlreadyProcessed_ShowsReloadMessage()
    {
        var client = new FakeModerationClient
        {
            ApproveException = new ApiException(HttpStatusCode.Conflict, "Invalid transition.", "listing_status_conflict")
        };
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items, Has.Count.EqualTo(1));
            Assert.That(
                viewModel.Message,
                Is.EqualTo("Tin này không còn ở trạng thái chờ duyệt. Tải lại hàng đợi để xem trạng thái mới."));
        });
    }

    [Test]
    public async Task Approve_WhenListingNoLongerExists_RemovesIt()
    {
        var client = new FakeModerationClient
        {
            ApproveException = new ApiException(HttpStatusCode.NotFound, "Listing not found.", "listing_not_found")
        };
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Empty));
            Assert.That(viewModel.Message, Does.StartWith("Tin này không còn tồn tại"));
        });
    }

    [Test]
    public async Task SignOut_ClearsItemsAndTransitionsToSignedOutState()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.That(viewModel.CanSignOut, Is.True);

        await viewModel.SignOutCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.SignedOut));
            Assert.That(viewModel.CanSignOut, Is.False);
        });
    }

    [Test]
    public async Task Approve_WhenTheLastVisibleItemIsRemoved_LoadsTheNextPage()
    {
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page("cursor-2", hasMore: true, Listing("Phòng A")));
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng B")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Select(item => item.Title), Is.EqualTo(new[] { "Phòng B" }));
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Content));
            Assert.That(client.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Approve_WhenApiForbids_ShowsDenied()
    {
        var client = new FakeModerationClient
        {
            ApproveException = new ApiException(HttpStatusCode.Forbidden, "Forbidden.", "forbidden")
        };
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng A")));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Denied));
    }

    [Test]
    public async Task SignIn_WhenInvoked_NavigatesToLogin()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeModerationClient(),
            new FakeTokenStore(),
            navigationService: navigationService);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoToLoginCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SelectPublishedTabCommand_LoadsPublishedListingsAndUpdatesTabFlags()
    {
        // Arrange
        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng chờ duyệt")));
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng đã duyệt") with { Status = ListingStatus.Published }));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.SelectPublishedTabCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedStatus, Is.EqualTo(ListingStatus.Published));
            Assert.That(viewModel.IsPublishedTab, Is.True);
            Assert.That(viewModel.IsPendingTab, Is.False);
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Content));
            Assert.That(viewModel.Items.Select(item => item.Title), Is.EqualTo(new[] { "Phòng đã duyệt" }));
            Assert.That(viewModel.Items[0].IsPublished, Is.True);
            Assert.That(viewModel.Items[0].StatusText, Is.EqualTo("Đã duyệt · Đang đăng"));
            Assert.That(viewModel.QueueSummaryText, Is.EqualTo("1 phòng đã duyệt công khai"));
            Assert.That(client.StatusRequests, Is.EqualTo(new[] { (ListingStatus.Published, 20, (string?)null) }));
        });
    }

    [Test]
    public async Task OpenLocationMapCommand_WithCoordinates_NavigatesToDirections()
    {
        // Arrange
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeModerationClient(),
            SignedInAs(AppUserRole.Moderator),
            navigationService: navigationService);
        var itemWithCoordinates = new ModerationQueueItem(
            Listing("Phòng có GPS") with { Lat = 10.776889, Lng = 106.700806 });
        var itemWithoutCoordinates = new ModerationQueueItem(
            Listing("Phòng chưa có GPS") with { Lat = 0, Lng = 0 });

        // Act
        await viewModel.OpenLocationMapCommand.ExecuteAsync(itemWithCoordinates);
        await viewModel.OpenLocationMapCommand.ExecuteAsync(itemWithoutCoordinates);

        // Assert
        Assert.That(
            navigationService.DirectionsRequests,
            Is.EqualTo(new[] { (10.776889, 106.700806, (double?)null, (double?)null) }));
    }

    [Test]
    public void ModerationQueueItem_MapsPhotosPriceSpecsAndLocation()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Arrange
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var listingId = Guid.NewGuid();
            var secondPhoto = new ListingPhotoDto(
                Guid.NewGuid(), listingId, "/media/second.jpg", "/media/second-thumb.jpg",
                800, 600, ListingPhotoKind.Photo, null, SortOrder: 0, IsCover: false);
            var coverPhoto = new ListingPhotoDto(
                Guid.NewGuid(), listingId, "/media/cover.jpg", "/media/cover-thumb.jpg",
                800, 600, ListingPhotoKind.Photo, null, SortOrder: 1, IsCover: true);
            var dto = new ModerationListingDto(
                listingId,
                "Studio trung tâm",
                Guid.NewGuid(),
                ListingStatus.PendingReview,
                new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc),
                Description: "  Gần trường đại học  ",
                Price: 3_500_000,
                Deposit: 3_500_000,
                AreaM2: 24.5,
                RoomType: RoomType.Studio,
                Bedrooms: 1,
                Address: "12 Nguyễn Huệ, Quận 1",
                Lat: 10.776889,
                Lng: 106.700806,
                LandlordName: "Nguyễn Văn A",
                LandlordPhone: "0901234567",
                Photos: [secondPhoto, coverPhoto]);

            // Act
            var item = new ModerationQueueItem(dto);

            // Assert
            var baseUri = new Uri(ApiConstants.BaseAddress);
            Assert.Multiple(() =>
            {
                Assert.That(item.IsPending, Is.True);
                Assert.That(item.StatusText, Is.EqualTo("Chờ duyệt"));
                Assert.That(item.PriceText, Is.EqualTo("3,500,000 đ/tháng"));
                Assert.That(item.DepositText, Is.EqualTo("Tiền cọc: 3,500,000 đ"));
                Assert.That(item.SpecsText, Is.EqualTo("Studio · 24.5 m² · 1 PN"));
                Assert.That(item.LandlordText, Is.EqualTo("Chủ trọ: Nguyễn Văn A · SĐT: 0901234567"));
                Assert.That(item.Address, Is.EqualTo("12 Nguyễn Huệ, Quận 1"));
                Assert.That(item.HasCoordinates, Is.True);
                Assert.That(item.CoordinatesText, Is.EqualTo("Tọa độ GPS: 10.776889, 106.700806"));
                Assert.That(item.Description, Is.EqualTo("Gần trường đại học"));
                Assert.That(item.HasDescription, Is.True);
                Assert.That(item.CoverPhotoUrl, Is.EqualTo(new Uri(baseUri, "/media/cover.jpg").AbsoluteUri));
                Assert.That(item.HasPhotos, Is.True);
                Assert.That(item.MissingPhotos, Is.False);
                Assert.That(item.HasMultiplePhotos, Is.True);
                Assert.That(item.PhotoCountText, Is.EqualTo("2 ảnh"));
                Assert.That(item.Photos.Select(photo => photo.Url), Is.EqualTo(new[]
                {
                    new Uri(baseUri, "/media/cover-thumb.jpg").AbsoluteUri,
                    new Uri(baseUri, "/media/second-thumb.jpg").AbsoluteUri
                }));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task SelectPendingThenPublishedTab_WhilePendingRequestInFlight_CancelsAndIgnoresStalePendingResponse()
    {
        // Arrange
        var pendingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<ModerationListingQueueDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedPendingToken = default;
        var client = new FakeModerationClient
        {
            PendingOverride = cancellationToken =>
            {
                observedPendingToken = cancellationToken;
                pendingStarted.TrySetResult();
                return pendingResponse.Task;
            }
        };
        client.Pages.Enqueue(Page(null, hasMore: false, Listing("Phòng đã duyệt") with { Status = ListingStatus.Published }));
        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        viewModel.SelectedStatus = ListingStatus.Published;

        // Act
        var pendingTabTask = viewModel.SelectPendingTabCommand.ExecuteAsync(null);
        await pendingStarted.Task;
        await viewModel.SelectPublishedTabCommand.ExecuteAsync(null);
        pendingResponse.SetResult(Page(null, hasMore: false, Listing("Phòng chờ duyệt cũ")));
        await pendingTabTask;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(observedPendingToken.IsCancellationRequested, Is.True);
            Assert.That(viewModel.SelectedStatus, Is.EqualTo(ListingStatus.Published));
            Assert.That(viewModel.IsPublishedTab, Is.True);
            Assert.That(viewModel.IsPendingTab, Is.False);
            Assert.That(viewModel.State, Is.EqualTo(ModerationQueueState.Content));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.Items.Select(item => item.Title), Is.EqualTo(new[] { "Phòng đã duyệt" }));
            Assert.That(viewModel.QueueSummaryText, Is.EqualTo("1 phòng đã duyệt công khai"));
        });
    }

    [Test]
    public void ModerationQueueItem_MoreThanFourPhotos_PopulatesHeroSecondarySlotsAndExtraPhotosBadge()
    {
        // Arrange
        var listingId = Guid.NewGuid();
        var photos = Enumerable.Range(0, 6)
            .Select(index => new ListingPhotoDto(
                Guid.NewGuid(),
                listingId,
                $"/media/photo-{index}.jpg",
                $"/media/photo-{index}-thumb.jpg",
                800,
                600,
                ListingPhotoKind.Photo,
                null,
                SortOrder: index,
                IsCover: index == 0))
            .ToArray();
        var dto = Listing("Căn hộ 6 ảnh") with { Id = listingId, Photos = photos };

        // Act
        var item = new ModerationQueueItem(dto);

        // Assert
        var baseUri = new Uri(ApiConstants.BaseAddress);
        Assert.Multiple(() =>
        {
            Assert.That(item.CoverPhotoUrl, Is.EqualTo(new Uri(baseUri, "/media/photo-0.jpg").AbsoluteUri));
            Assert.That(item.HeroDisplayUrl, Is.EqualTo(new Uri(baseUri, "/media/photo-0-thumb.jpg").AbsoluteUri));
            Assert.That(item.HasSecondaryPhoto1, Is.True);
            Assert.That(item.SecondaryPhoto1Url, Is.EqualTo(new Uri(baseUri, "/media/photo-1-thumb.jpg").AbsoluteUri));
            Assert.That(item.HasSecondaryPhoto2, Is.True);
            Assert.That(item.SecondaryPhoto2Url, Is.EqualTo(new Uri(baseUri, "/media/photo-2-thumb.jpg").AbsoluteUri));
            Assert.That(item.HasSecondaryPhoto3, Is.True);
            Assert.That(item.SecondaryPhoto3Url, Is.EqualTo(new Uri(baseUri, "/media/photo-3-thumb.jpg").AbsoluteUri));
            Assert.That(item.HasExtraPhotos, Is.True);
            Assert.That(item.ExtraPhotosText, Is.EqualTo("+2 ảnh"));
            Assert.That(item.PhotoCountText, Is.EqualTo("6 ảnh"));
        });
    }

    [Test]
    public async Task ApproveThenSelectPublishedTab_PlacesNewlyApprovedListingAtTopWithoutDuplicates()
    {
        // Arrange
        var newlyApprovedFirst = Listing("Phòng mới đăng 1");
        var newlyApprovedLatest = Listing("Phòng mới đăng 2");
        var remainingPending = Listing("Phòng chờ khác");
        var olderPublished = Listing("Phòng đã duyệt cũ") with
        {
            Status = ListingStatus.Published,
            UpdatedAt = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc)
        };
        var serverApprovedFirst = newlyApprovedFirst with
        {
            Status = ListingStatus.Published,
            UpdatedAt = new DateTime(2026, 11, 25, 18, 20, 0, DateTimeKind.Utc)
        };

        var client = new FakeModerationClient();
        client.Pages.Enqueue(Page(null, hasMore: false, newlyApprovedFirst, newlyApprovedLatest, remainingPending));
        client.Pages.Enqueue(Page(null, hasMore: false, olderPublished, serverApprovedFirst));

        var viewModel = CreateViewModel(client, SignedInAs(AppUserRole.Admin));
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);
        await viewModel.ApproveCommand.ExecuteAsync(viewModel.Items[0]);
        await viewModel.SelectPublishedTabCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsPublishedTab, Is.True);
            Assert.That(
                viewModel.Items.Select(item => item.Title),
                Is.EqualTo(new[] { "Phòng mới đăng 2", "Phòng mới đăng 1", "Phòng đã duyệt cũ" }));
            Assert.That(viewModel.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(viewModel.Items[0].IsPublished, Is.True);
            Assert.That(viewModel.Items[0].CreatedText, Does.StartWith("Đã duyệt lúc "));
            Assert.That(viewModel.Items[1].IsPublished, Is.True);
            Assert.That(viewModel.Items[1].CreatedText, Does.StartWith("Đã duyệt lúc "));
        });
    }

    private static ModerationQueueViewModel CreateViewModel(
        FakeModerationClient client,
        FakeTokenStore tokenStore,
        FakeAlertPrompt? prompt = null,
        FakeNavigationService? navigationService = null,
        FakeErrorHandler? errorHandler = null)
        => new(
            client,
            new AuthService(null!, tokenStore),
            navigationService ?? new FakeNavigationService(),
            prompt ?? new FakeAlertPrompt(),
            errorHandler ?? new FakeErrorHandler());

    private static FakeTokenStore SignedInAs(AppUserRole role)
        => new() { AccessToken = "test-token", Role = role };

    private static ModerationListingDto Listing(string title)
        => new(
            Guid.NewGuid(),
            title,
            Guid.NewGuid(),
            ListingStatus.PendingReview,
            new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc));

    private static ModerationListingQueueDto Page(string? nextCursor, bool hasMore, params ModerationListingDto[] items)
        => new(items, new PaginationInfo(20, nextCursor, hasMore));

    private sealed class FakeModerationClient : IModerationClient
    {
        public Queue<ModerationListingQueueDto> Pages { get; } = new();

        public List<(int Limit, string? Cursor)> Requests { get; } = new();

        public List<(ListingStatus Status, int Limit, string? Cursor)> StatusRequests { get; } = [];

        public List<Guid> Approved { get; } = new();

        public Exception? ListException { get; set; }

        public Exception? ApproveException { get; set; }

        public Func<CancellationToken, Task<ModerationListingQueueDto>>? PendingOverride { get; set; }

        public Task<ModerationListingQueueDto> GetPendingListingsAsync(
            int limit = 20,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((limit, cursor));

            if (PendingOverride is not null)
            {
                return PendingOverride(cancellationToken);
            }

            if (ListException is { } exception)
            {
                return Task.FromException<ModerationListingQueueDto>(exception);
            }

            return Task.FromResult(Pages.Dequeue());
        }

        public Task<ModerationListingQueueDto> GetListingsAsync(
            ListingStatus status,
            int limit = 20,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            StatusRequests.Add((status, limit, cursor));

            if (ListException is { } exception)
            {
                return Task.FromException<ModerationListingQueueDto>(exception);
            }

            return Task.FromResult(Pages.Dequeue());
        }

        public Task ApproveAsync(Guid listingId, CancellationToken cancellationToken = default)
        {
            if (ApproveException is { } exception)
            {
                return Task.FromException(exception);
            }

            Approved.Add(listingId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> Errors { get; } = [];

        public void HandleError(Exception ex) => Errors.Add(ex);
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; set; }

        public AppUserRole? Role { get; set; }

        public Exception? RoleException { get; set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AccessToken);

        public Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
            => RoleException is { } exception
                ? Task.FromException<AppUserRole?>(exception)
                : Task.FromResult(Role);

        public Task SaveAsync(
            string accessToken,
            DateTime expiresAt,
            AppUserRole role,
            CancellationToken cancellationToken = default)
        {
            AccessToken = accessToken;
            Role = role;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            Role = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAlertPrompt : IAlertPrompt
    {
        public bool ConfirmResult { get; set; } = true;

        public int ConfirmCalls { get; private set; }

        public Task ShowAsync(
            string title,
            string message,
            string dismissText = "OK",
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string acceptText,
            string cancelText,
            CancellationToken cancellationToken = default)
        {
            ConfirmCalls++;
            return Task.FromResult(ConfirmResult);
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public int GoToLoginCount { get; private set; }

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null)
            => throw new NotSupportedException("The moderation screen does not open the listing editor.");

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The moderation screen does not open room details.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The moderation screen does not open the registration screen.");

        public List<AppUserRole?> RoleRootRoles { get; } = [];

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public List<(double DestinationLat, double DestinationLng, double? OriginLat, double? OriginLng)> DirectionsRequests { get; } = [];

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
        {
            DirectionsRequests.Add((destinationLat, destinationLng, originLat, originLng));
            return Task.CompletedTask;
        }

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The moderation screen does not open room comparison.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The moderation screen does not navigate back.");
    }
}
