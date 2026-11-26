using System.Net;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MyListingsViewModelTests
{
    [Test]
    public async Task LoadCommand_SignedInLandlord_PopulatesListingsFilterCountsAndSummary()
    {
        // Arrange
        var client = new FakeListingManagementClient();
        client.Listings.AddRange(
        [
            CreateListing("Phòng A", ListingStatus.Published),
            CreateListing("Phòng B", ListingStatus.Published),
            CreateListing("Phòng C", ListingStatus.PendingReview),
            CreateListing("Phòng D", ListingStatus.Hidden)
        ]);
        var viewModel = CreateViewModel(client, SignedInLandlord());

        // Act
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAuthenticated, Is.True);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.Message, Is.Empty);
            Assert.That(viewModel.HasAnyListings, Is.True);
            Assert.That(viewModel.NoOwnedListings, Is.False);
            Assert.That(viewModel.IsFilteredEmpty, Is.False);
            Assert.That(viewModel.Listings.Select(item => item.Title), Is.EqualTo(new[] { "Phòng A", "Phòng B", "Phòng C", "Phòng D" }));
            Assert.That(viewModel.DashboardSummaryText, Is.EqualTo("Tổng 4 phòng · 2 đã duyệt · 1 chờ duyệt"));
            Assert.That(FilterFor(viewModel, null).Count, Is.EqualTo(4));
            Assert.That(FilterFor(viewModel, null).IsSelected, Is.True);
            Assert.That(FilterFor(viewModel, ListingStatus.Published).BaseLabel, Is.EqualTo("Đã duyệt"));
            Assert.That(FilterFor(viewModel, ListingStatus.Published).Count, Is.EqualTo(2));
            Assert.That(FilterFor(viewModel, ListingStatus.PendingReview).Count, Is.EqualTo(1));
            Assert.That(FilterFor(viewModel, ListingStatus.Hidden).Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SelectStatusFilter_FiltersListingsAndMarksFilteredEmptyWhenNoMatches()
    {
        // Arrange
        var client = new FakeListingManagementClient();
        client.Listings.AddRange(
        [
            CreateListing("Phòng đang đăng", ListingStatus.Published),
            CreateListing("Phòng chờ duyệt", ListingStatus.PendingReview)
        ]);
        var viewModel = CreateViewModel(client, SignedInLandlord());
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        FilterFor(viewModel, ListingStatus.Published).SelectCommand.Execute(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedStatusFilter, Is.EqualTo(ListingStatus.Published));
            Assert.That(viewModel.Listings.Select(item => item.Title), Is.EqualTo(new[] { "Phòng đang đăng" }));
            Assert.That(viewModel.IsFilteredEmpty, Is.False);
            Assert.That(FilterFor(viewModel, ListingStatus.Published).IsSelected, Is.True);
            Assert.That(FilterFor(viewModel, null).IsSelected, Is.False);
        });

        // Act
        viewModel.SelectStatusFilter(ListingStatus.Hidden);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedStatusFilter, Is.EqualTo(ListingStatus.Hidden));
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.HasAnyListings, Is.True);
            Assert.That(viewModel.NoOwnedListings, Is.False);
            Assert.That(viewModel.IsFilteredEmpty, Is.True);
            Assert.That(FilterFor(viewModel, ListingStatus.Hidden).IsSelected, Is.True);
        });
    }

    [Test]
    public async Task CreateAndEditCommands_NavigateToEditor_AndIgnoreRemovedListings()
    {
        // Arrange
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeListingManagementClient(), SignedInLandlord(), navigation: navigation);
        var editableItem = new OwnedListingItem(CreateListing("Phòng A", ListingStatus.Published));
        var removedItem = new OwnedListingItem(CreateListing("Phòng vi phạm", ListingStatus.Removed));

        // Act
        await viewModel.CreateCommand.ExecuteAsync(null);
        await viewModel.EditCommand.ExecuteAsync(editableItem);
        await viewModel.EditCommand.ExecuteAsync(removedItem);
        await viewModel.EditCommand.ExecuteAsync(null);

        // Assert
        Assert.That(navigation.EditorListingIds, Is.EqualTo(new Guid?[] { null, editableItem.Id }));
    }

    [Test]
    public async Task ManagePhotosCommand_NavigatesToThePhotoManagerOfTheListing()
    {
        // Arrange
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeListingManagementClient(), SignedInLandlord(), navigation: navigation);
        var item = new OwnedListingItem(CreateListing("Phòng A", ListingStatus.Published));

        // Act
        await viewModel.ManagePhotosCommand.ExecuteAsync(item);
        await viewModel.ManagePhotosCommand.ExecuteAsync(null);

        // Assert
        Assert.That(navigation.PhotoManagerListingIds, Is.EqualTo(new[] { item.Id }));
    }

    [Test]
    public async Task PublishCommand_HiddenListing_TransitionsToPendingReviewAndUpdatesCounts()
    {
        // Arrange
        var hiddenListing = CreateListing("Phòng gỡ tạm", ListingStatus.Hidden);
        var publishedListing = CreateListing("Phòng đang đăng", ListingStatus.Published);
        var client = new FakeListingManagementClient();
        client.Listings.AddRange([hiddenListing, publishedListing]);
        var viewModel = CreateViewModel(client, SignedInLandlord());
        await viewModel.LoadCommand.ExecuteAsync(null);
        var hiddenItem = viewModel.Listings.Single(item => item.Id == hiddenListing.Id);

        // Act
        await viewModel.PublishCommand.ExecuteAsync(hiddenItem);

        // Assert
        var updatedItem = viewModel.Listings.Single(item => item.Id == hiddenListing.Id);
        Assert.Multiple(() =>
        {
            Assert.That(client.PublishedIds, Is.EqualTo(new[] { hiddenListing.Id }));
            Assert.That(updatedItem.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(updatedItem.CanPublish, Is.False);
            Assert.That(updatedItem.CanUnpublish, Is.True);
            Assert.That(FilterFor(viewModel, ListingStatus.Hidden).Count, Is.Zero);
            Assert.That(FilterFor(viewModel, ListingStatus.PendingReview).Count, Is.EqualTo(1));
            Assert.That(FilterFor(viewModel, ListingStatus.Published).Count, Is.EqualTo(1));
            Assert.That(viewModel.DashboardSummaryText, Is.EqualTo("Tổng 2 phòng · 1 đã duyệt · 1 chờ duyệt"));
            Assert.That(viewModel.Message, Is.EqualTo("Đã gửi duyệt lại tin đăng."));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task UnpublishCommand_ConfirmationDeclinedAndConfirmed_UpdatesStatusOnlyWhenConfirmed()
    {
        // Arrange
        var publishedListing = CreateListing("Phòng A", ListingStatus.Published);
        var client = new FakeListingManagementClient();
        client.Listings.Add(publishedListing);
        var alerts = new FakeAlertPrompt { ConfirmResult = false };
        var viewModel = CreateViewModel(client, SignedInLandlord(), alerts: alerts);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.UnpublishCommand.ExecuteAsync(viewModel.Listings[0]);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(client.UnpublishedIds, Is.Empty);
            Assert.That(viewModel.Listings[0].Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(FilterFor(viewModel, ListingStatus.Published).Count, Is.EqualTo(1));
            Assert.That(FilterFor(viewModel, ListingStatus.Hidden).Count, Is.Zero);
        });

        // Act
        alerts.ConfirmResult = true;
        await viewModel.UnpublishCommand.ExecuteAsync(viewModel.Listings[0]);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(2));
            Assert.That(client.UnpublishedIds, Is.EqualTo(new[] { publishedListing.Id }));
            Assert.That(viewModel.Listings[0].Status, Is.EqualTo(ListingStatus.Hidden));
            Assert.That(FilterFor(viewModel, ListingStatus.Published).Count, Is.Zero);
            Assert.That(FilterFor(viewModel, ListingStatus.Hidden).Count, Is.EqualTo(1));
            Assert.That(viewModel.Message, Is.EqualTo("Đã gỡ đăng tin."));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task DeleteCommand_ConfirmationDeclinedAndConfirmed_RemovesListingAndUpdatesCountsOnlyWhenConfirmed()
    {
        // Arrange
        var publishedListing = CreateListing("Phòng A", ListingStatus.Published);
        var client = new FakeListingManagementClient();
        client.Listings.Add(publishedListing);
        var alerts = new FakeAlertPrompt { ConfirmResult = false };
        var viewModel = CreateViewModel(client, SignedInLandlord(), alerts: alerts);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(viewModel.Listings[0]);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(client.DeletedIds, Is.Empty);
            Assert.That(viewModel.Listings, Has.Count.EqualTo(1));
            Assert.That(viewModel.HasAnyListings, Is.True);
        });

        // Act
        alerts.ConfirmResult = true;
        await viewModel.DeleteCommand.ExecuteAsync(viewModel.Listings[0]);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(2));
            Assert.That(client.DeletedIds, Is.EqualTo(new[] { publishedListing.Id }));
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.HasAnyListings, Is.False);
            Assert.That(viewModel.NoOwnedListings, Is.True);
            Assert.That(viewModel.StatusFilters.All(filter => filter.Count == 0), Is.True);
            Assert.That(viewModel.DashboardSummaryText, Is.EqualTo("Tổng 0 phòng · 0 đã duyệt · 0 chờ duyệt"));
            Assert.That(viewModel.Message, Is.EqualTo("Đã xóa tin đăng."));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task DeleteCommand_Unauthorized_MarksSignedOut()
    {
        // Arrange
        var listing = CreateListing("Phòng A", ListingStatus.Published);
        var client = new FakeListingManagementClient
        {
            DeleteError = new ListingManagementException(
                HttpStatusCode.Unauthorized,
                "Phiên đăng nhập đã hết hạn.",
                "unauthorized",
                new Dictionary<string, string[]>())
        };
        client.Listings.Add(listing);
        var alerts = new FakeAlertPrompt { ConfirmResult = true };
        var viewModel = CreateViewModel(client, SignedInLandlord(), alerts: alerts);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(viewModel.Listings[0]);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Message, Is.EqualTo("Phiên đăng nhập đã hết hạn."));
            Assert.That(viewModel.Listings, Has.Count.EqualTo(1));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task SignOutCommand_ClearsListingsResetsFiltersAndNavigatesToGuestRoot()
    {
        // Arrange
        var client = new FakeListingManagementClient();
        client.Listings.AddRange(
        [
            CreateListing("Phòng A", ListingStatus.Published),
            CreateListing("Phòng B", ListingStatus.Hidden)
        ]);
        var tokenStore = SignedInLandlord();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(client, tokenStore, navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectStatusFilter(ListingStatus.Hidden);

        // Act
        await viewModel.SignOutCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(tokenStore.ClearCalls, Is.EqualTo(1));
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Listings, Is.Empty);
            Assert.That(viewModel.HasAnyListings, Is.False);
            Assert.That(viewModel.NoOwnedListings, Is.True);
            Assert.That(viewModel.SelectedStatusFilter, Is.Null);
            Assert.That(FilterFor(viewModel, null).IsSelected, Is.True);
            Assert.That(viewModel.StatusFilters.All(filter => filter.Count == 0), Is.True);
            Assert.That(navigation.RoleRootRoles, Is.EqualTo(new AppUserRole?[] { null }));
        });
    }

    [Test]
    public void OwnedListingItem_PublishedStatus_MarksPublishedAndShowsCompletedText()
    {
        // Arrange
        var listing = CreateListing("Phòng đã duyệt", ListingStatus.Published);

        // Act
        var item = new OwnedListingItem(listing);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(item.IsPublished, Is.True);
            Assert.That(item.IsPendingReview, Is.False);
            Assert.That(item.StatusText, Is.EqualTo("Đã duyệt · Hoàn thành"));
        });
    }

    [Test]
    public async Task AttachAndDetachAuthSession_SessionChanges_ReloadsAndClearsOnlyWhileAttached()
    {
        // Arrange
        var client = new FakeListingManagementClient();
        client.Listings.Add(CreateListing("Phòng của chủ trọ", ListingStatus.Published));
        var tokenStore = SignedInLandlord();
        var auth = new AuthService(null!, tokenStore);
        var viewModel = new MyListingsViewModel(
            client,
            auth,
            new FakeNavigationService(),
            new FakeAlertPrompt());

        // Act: session event before AttachAuthSession must not trigger a load
        await auth.EnsureSignedInAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(client.GetMineCalls, Is.Zero);
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Listings, Is.Empty);
        });

        // Act: attach (idempotent) -> landlord session reloads -> sign-out clears
        tokenStore.AccessToken = "second-token";
        tokenStore.Role = AppUserRole.Landlord;
        viewModel.AttachAuthSession();
        viewModel.AttachAuthSession();
        await auth.EnsureSignedInAsync();
        if (viewModel.LoadCommand.ExecutionTask is { } loadTask)
        {
            await loadTask;
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(client.GetMineCalls, Is.EqualTo(1));
            Assert.That(viewModel.IsAuthenticated, Is.True);
            Assert.That(viewModel.Listings.Select(item => item.Title), Is.EqualTo(new[] { "Phòng của chủ trọ" }));
        });

        // Act: sign out while attached clears state
        await auth.SignOutAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Listings, Is.Empty);
        });

        // Act: detach -> subsequent session changes no longer reload
        viewModel.DetachAuthSession();
        tokenStore.AccessToken = "third-token";
        tokenStore.Role = AppUserRole.Landlord;
        await auth.EnsureSignedInAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(client.GetMineCalls, Is.EqualTo(1));
            Assert.That(viewModel.IsAuthenticated, Is.False);
            Assert.That(viewModel.Listings, Is.Empty);
        });
    }

    private static MyListingsViewModel CreateViewModel(
        FakeListingManagementClient client,
        FakeTokenStore tokenStore,
        FakeNavigationService? navigation = null,
        FakeAlertPrompt? alerts = null)
        => new(
            client,
            new AuthService(null!, tokenStore),
            navigation ?? new FakeNavigationService(),
            alerts ?? new FakeAlertPrompt());

    private static FakeTokenStore SignedInLandlord()
        => new() { AccessToken = "test-token", Role = AppUserRole.Landlord };

    private static OwnedListingFilterOption FilterFor(MyListingsViewModel viewModel, ListingStatus? status)
        => viewModel.StatusFilters.Single(filter => filter.Status == status);

    private static ManagedListingDto CreateListing(string title, ListingStatus status)
    {
        var id = Guid.NewGuid();
        var photo = new ListingPhotoDto(
            Guid.NewGuid(), id, "/media/room.jpg", "/media/room-thumb.jpg",
            800, 600, ListingPhotoKind.Photo, null, 0, true);
        return new ManagedListingDto(
            id,
            title,
            "Mô tả phòng trọ",
            3_000_000,
            3_000_000,
            22,
            RoomType.Studio,
            1,
            "15 Đường Đại Học",
            10.76,
            106.68,
            status,
            new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc),
            [],
            [],
            "Chủ trọ",
            [photo]);
    }

    private sealed class FakeListingManagementClient : IListingManagementClient
    {
        public List<ManagedListingDto> Listings { get; } = [];
        public int GetMineCalls { get; private set; }
        public List<Guid> PublishedIds { get; } = [];
        public List<Guid> UnpublishedIds { get; } = [];
        public List<Guid> DeletedIds { get; } = [];
        public Exception? GetMineError { get; set; }
        public Exception? PublishError { get; set; }
        public Exception? UnpublishError { get; set; }
        public Exception? DeleteError { get; set; }

        public Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(CancellationToken cancellationToken = default)
        {
            GetMineCalls++;
            return GetMineError is { } error
                ? Task.FromException<IReadOnlyList<ManagedListingDto>>(error)
                : Task.FromResult<IReadOnlyList<ManagedListingDto>>(Listings.ToArray());
        }

        public Task<ManagedListingDto> GetManagedAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The MyListings dashboard does not fetch single managed listings.");

        public Task<ManagedListingDto> CreateAsync(ListingFormSubmission form, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The MyListings dashboard does not create listings directly.");

        public Task<ManagedListingDto> UpdateAsync(Guid id, ListingFormSubmission form, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The MyListings dashboard does not update listings directly.");

        public Task<ManagedListingDto> PublishAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (PublishError is { } error)
            {
                return Task.FromException<ManagedListingDto>(error);
            }

            PublishedIds.Add(id);
            var index = Listings.FindIndex(item => item.Id == id);
            var updated = Listings[index] with { Status = ListingStatus.PendingReview };
            Listings[index] = updated;
            return Task.FromResult(updated);
        }

        public Task<ManagedListingDto> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (UnpublishError is { } error)
            {
                return Task.FromException<ManagedListingDto>(error);
            }

            UnpublishedIds.Add(id);
            var index = Listings.FindIndex(item => item.Id == id);
            var updated = Listings[index] with { Status = ListingStatus.Hidden };
            Listings[index] = updated;
            return Task.FromResult(updated);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (DeleteError is { } error)
            {
                return Task.FromException(error);
            }

            DeletedIds.Add(id);
            Listings.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task<HouseRulesDto> UpdateHouseRulesAsync(
            Guid id,
            UpdateHouseRulesRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The MyListings dashboard does not update house rules.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; set; }
        public AppUserRole? Role { get; set; }
        public int ClearCalls { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AccessToken);

        public Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Role);

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
            ClearCalls++;
            AccessToken = null;
            Role = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<Guid?> EditorListingIds { get; } = [];
        public List<Guid> PhotoManagerListingIds { get; } = [];
        public List<AppUserRole?> RoleRootRoles { get; } = [];
        public int GoToLoginCalls { get; private set; }

        public Task GoToListingEditorAsync(Guid? listingId = null)
        {
            EditorListingIds.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToListingPhotoManagerAsync(Guid listingId)
        {
            PhotoManagerListingIds.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public Task GoToLoginAsync()
        {
            GoToLoginCalls++;
            return Task.CompletedTask;
        }

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The MyListings dashboard does not open room details.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The MyListings dashboard does not open registration.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The MyListings dashboard does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The MyListings dashboard does not open comparison.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The MyListings dashboard does not navigate back.");
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
}
