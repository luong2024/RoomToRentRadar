using System.Net;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Features.ListingManagement.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MyListingsViewModelTests
{
    [Test]
    public async Task LoadCommand_WhenSignedIn_ListsEveryListingWithItsModerationStatus()
    {
        var published = CreateListing(ListingStatus.Published, "Phòng đã đăng", price: 3_000_000);
        var pending = CreateListing(ListingStatus.PendingReview, "Phòng chờ duyệt");
        var hidden = CreateListing(ListingStatus.Hidden, "Phòng đã gỡ");
        var removed = CreateListing(ListingStatus.Removed, "Phòng bị xóa");
        var client = new FakeClient();
        client.Listings.AddRange([published, pending, hidden, removed]);
        var viewModel = CreateViewModel(client, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Listings, Has.Count.EqualTo(4));
            Assert.That(viewModel.Listings[0].StatusText, Is.EqualTo("Đang đăng"));
            Assert.That(viewModel.Listings[0].PriceText, Is.EqualTo($"{3_000_000L:N0} đ/tháng"));
            Assert.That(viewModel.Listings[0].CanUnpublish, Is.True);
            Assert.That(viewModel.Listings[1].StatusText, Is.EqualTo("Chờ duyệt"));
            Assert.That(viewModel.Listings[1].CanUnpublish, Is.True);
            Assert.That(viewModel.Listings[2].StatusText, Is.EqualTo("Đã gỡ đăng"));
            Assert.That(viewModel.Listings[2].CanUnpublish, Is.False);
            Assert.That(viewModel.Listings[3].StatusText, Is.EqualTo("Đã bị xóa bởi kiểm duyệt"));
            Assert.That(viewModel.Listings[3].CanUnpublish, Is.False);
            Assert.That(viewModel.IsAuthenticated, Is.True);
            Assert.That(viewModel.Message, Is.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task LoadCommand_WhenNotSignedIn_ShowsSignInPromptWithoutCallingApi()
    {
        var client = new FakeClient();
        client.Listings.Add(CreateListing(ListingStatus.Published, "Phòng đã đăng"));
        var viewModel = CreateViewModel(client, new FakeNavigationService(), signedIn: false);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.MineCalls, Is.Zero);
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.Message, Is.EqualTo("Đăng nhập để đăng và quản lý tin cho thuê của bạn."));
        });
    }

    [Test]
    public async Task LoadCommand_WhenApiFails_ShowsRetryMessage()
    {
        var client = new FakeClient { MineError = new HttpRequestException("Offline") };
        var viewModel = CreateViewModel(client, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Message, Is.EqualTo("Không thể tải tin của bạn. Kiểm tra kết nối và thử lại."));
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.IsAuthenticated, Is.True);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task LoadCommand_WhenUnauthorized_RequiresSignInAgain()
    {
        var client = new FakeClient { MineError = UnauthorizedProblem() };
        var viewModel = CreateViewModel(client, new FakeNavigationService());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Message, Is.EqualTo("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
        });
    }

    [Test]
    public async Task CreateCommand_OpensEditorWithoutListingId()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeClient(), navigation);

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.That(navigation.EditorRequests, Is.EqualTo(new Guid?[] { null }));
    }

    [Test]
    public async Task EditCommand_OpensEditorWithTheListingId()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeClient(), navigation);
        var item = new OwnedListingItem(CreateListing(ListingStatus.PendingReview, "Phòng chờ duyệt"));

        await viewModel.EditCommand.ExecuteAsync(item);

        Assert.That(navigation.EditorRequests, Is.EqualTo(new Guid?[] { item.Id }));
    }

    [Test]
    public async Task SignInCommand_OpensLogin()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeClient(), navigation);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigation.LoginCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UnpublishCommand_WhenSuccessful_HidesTheItemInPlaceAndReportsIt()
    {
        var client = new FakeClient();
        client.Listings.Add(CreateListing(ListingStatus.Published, "Phòng đã đăng"));
        var viewModel = CreateViewModel(client, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);
        var item = viewModel.Listings[0];

        await viewModel.UnpublishCommand.ExecuteAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(client.UnpublishCalls, Is.EqualTo(1));
            Assert.That(item.Status, Is.EqualTo(ListingStatus.Hidden));
            Assert.That(item.StatusText, Is.EqualTo("Đã gỡ đăng"));
            Assert.That(item.CanUnpublish, Is.False);
            Assert.That(viewModel.OperationMessage, Is.EqualTo("Đã gỡ đăng tin \"Phòng đã đăng\"."));
            Assert.That(viewModel.Message, Is.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task UnpublishCommand_WhenApiFails_KeepsTheItemAndShowsError()
    {
        var client = new FakeClient { UnpublishError = new HttpRequestException("Offline") };
        client.Listings.Add(CreateListing(ListingStatus.Published, "Phòng đã đăng"));
        var viewModel = CreateViewModel(client, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.UnpublishCommand.ExecuteAsync(viewModel.Listings[0]);

        Assert.Multiple(() =>
        {
            Assert.That(client.UnpublishCalls, Is.EqualTo(1));
            Assert.That(viewModel.Listings[0].Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(viewModel.Listings[0].CanUnpublish, Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Không thể gỡ tin. Kiểm tra kết nối và thử lại."));
            Assert.That(viewModel.OperationMessage, Is.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task UnpublishCommand_WhenListingIsGone_ReloadsTheList()
    {
        var client = new FakeClient
        {
            UnpublishError = new ListingManagementException(
                HttpStatusCode.NotFound,
                "Listing not found.",
                "listing_not_found",
                new Dictionary<string, string[]>())
        };
        client.Listings.Add(CreateListing(ListingStatus.Published, "Phòng đã đăng"));
        var viewModel = CreateViewModel(client, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.UnpublishCommand.ExecuteAsync(viewModel.Listings[0]);

        Assert.Multiple(() =>
        {
            Assert.That(client.MineCalls, Is.EqualTo(2));
            Assert.That(viewModel.Message, Is.EqualTo("Tin đăng không còn tồn tại. Danh sách đã được làm mới."));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task UnpublishCommand_WhenItemIsAlreadyHidden_SkipsTheApi()
    {
        var client = new FakeClient();
        client.Listings.Add(CreateListing(ListingStatus.Hidden, "Phòng đã gỡ"));
        var viewModel = CreateViewModel(client, new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.UnpublishCommand.ExecuteAsync(viewModel.Listings[0]);

        Assert.That(client.UnpublishCalls, Is.Zero);
    }

    private static MyListingsViewModel CreateViewModel(
        FakeClient client,
        FakeNavigationService navigation,
        bool signedIn = true)
        => new(client, new AuthService(null!, new FakeTokenStore(signedIn)), navigation);

    private static ListingManagementException UnauthorizedProblem()
        => new(HttpStatusCode.Unauthorized, "Unauthorized.", null, new Dictionary<string, string[]>());

    private static ManagedListingDto CreateListing(
        ListingStatus status,
        string title,
        long price = 2_500_000)
    {
        var id = Guid.NewGuid();
        var timestamp = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        return new ManagedListingDto(
            id,
            title,
            "Mô tả",
            price,
            0,
            20,
            RoomType.Studio,
            1,
            "12 Nguyễn Trãi",
            10.75,
            106.7,
            status,
            timestamp,
            timestamp,
            [],
            [],
            "Chủ trọ",
            []);
    }

    private sealed class FakeTokenStore(bool signedIn) : ITokenStore
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(signedIn ? "test-token" : null);
        public Task SaveAsync(string accessToken, DateTime expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<Guid?> EditorRequests { get; } = [];
        public int LoginCount { get; private set; }

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The management list does not open room details.");

        public Task GoToLoginAsync()
        {
            LoginCount++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null)
        {
            EditorRequests.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToDirectionsAsync(double destinationLat, double destinationLng, double? originLat, double? originLng)
            => throw new NotSupportedException("The management list does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The management list does not open room comparison.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The management list does not navigate back directly.");
    }

    private sealed class FakeClient : IListingManagementClient
    {
        public int MineCalls { get; private set; }
        public List<ManagedListingDto> Listings { get; } = [];
        public Exception? MineError { get; set; }
        public int UnpublishCalls { get; private set; }
        public Exception? UnpublishError { get; set; }

        public Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(CancellationToken cancellationToken = default)
        {
            MineCalls++;
            return MineError is { } error
                ? Task.FromException<IReadOnlyList<ManagedListingDto>>(error)
                : Task.FromResult<IReadOnlyList<ManagedListingDto>>(Listings.ToList());
        }

        public Task<ManagedListingDto> GetManagedAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The management list does not open a single listing.");

        public Task<ManagedListingDto> CreateAsync(ListingFormSubmission form, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The management list does not create listings.");

        public Task<ManagedListingDto> UpdateAsync(Guid id, ListingFormSubmission form, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The management list does not update listings.");

        public Task<ManagedListingDto> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
        {
            UnpublishCalls++;
            if (UnpublishError is { } error) return Task.FromException<ManagedListingDto>(error);

            var index = Listings.FindIndex(listing => listing.Id == id);
            var hidden = Listings[index] with { Status = ListingStatus.Hidden };
            Listings[index] = hidden;
            return Task.FromResult(hidden);
        }
    }
}
