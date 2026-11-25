using System.Net;
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

        public List<Guid> Approved { get; } = new();

        public Exception? ListException { get; set; }

        public Exception? ApproveException { get; set; }

        public Task<ModerationListingQueueDto> GetPendingListingsAsync(
            int limit = 20,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((limit, cursor));

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

        public Task GoToRoleRootAsync(AppUserRole? role)
            => throw new NotSupportedException("The moderation screen does not navigate to a role root.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The moderation screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The moderation screen does not open room comparison.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The moderation screen does not navigate back.");
    }
}
