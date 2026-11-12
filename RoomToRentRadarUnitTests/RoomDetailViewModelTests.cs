using System.Globalization;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.RoomDetail.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomDetailViewModelTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");

    [Test]
    public async Task LoadAsync_WhenDetailFound_PopulatesListingAndShowsContent()
    {
        var createdAt = new DateTime(2025, 3, 14, 8, 30, 0, DateTimeKind.Utc);
        var detail = CreateDetail(createdAt);
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, detail));
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        var expectedPostedOn = createdAt.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var expectedUpdatedOn = createdAt.AddDays(2).ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var listing = viewModel.Listing;
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Content));
            Assert.That(listing, Is.Not.Null);
            Assert.That(listing?.Id, Is.EqualTo(ListingId));
            Assert.That(listing?.Title, Is.EqualTo("Sunny room"));
            Assert.That(listing?.Description, Is.EqualTo("Bright and quiet room near the market."));
            Assert.That(listing?.Price, Is.EqualTo(1_500_000));
            Assert.That(listing?.Deposit, Is.EqualTo(3_000_000));
            Assert.That(listing?.AreaM2, Is.EqualTo(25));
            Assert.That(listing?.RoomType, Is.EqualTo(RoomType.Apartment));
            Assert.That(listing?.Bedrooms, Is.EqualTo(1));
            Assert.That(listing?.Address, Is.EqualTo("1 Main St"));
            Assert.That(listing?.Status, Is.EqualTo(ListingStatus.Published));
            Assert.That(listing?.CreatedAt, Is.EqualTo(createdAt));
            Assert.That(listing?.UpdatedAt, Is.EqualTo(createdAt.AddDays(2)));
            Assert.That(listing?.LandlordName, Is.EqualTo("Landlord An"));
            Assert.That(viewModel.PostedOnText, Is.EqualTo(expectedPostedOn));
            Assert.That(viewModel.UpdatedOnText, Is.EqualTo(expectedUpdatedOn));
            Assert.That(viewModel.StatusText, Is.EqualTo("Available"));
            Assert.That(viewModel.RoomTypeText, Is.EqualTo("Apartment"));
            Assert.That(viewModel.AmenityLabels, Is.EqualTo(new[] { "Wi-Fi", "Air conditioning" }));
            Assert.That(viewModel.Rules, Is.EqualTo(new[] { "No smoking", "Quiet hours after 22:00" }));
            Assert.That(viewModel.HasAmenities, Is.True);
            Assert.That(viewModel.HasRules, Is.True);
            Assert.That(viewModel.LandlordPhone, Is.Empty);
            Assert.That(viewModel.IsPhoneVisible, Is.False);
            Assert.That(viewModel.IsSignInPromptVisible, Is.True);
            Assert.That(viewModel.IsContentVisible, Is.True);
            Assert.That(viewModel.IsLoadingVisible, Is.False);
            Assert.That(viewModel.IsUnavailableVisible, Is.False);
            Assert.That(viewModel.IsNotFoundVisible, Is.False);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedIds[0], Is.EqualTo(ListingId));
            Assert.That(apiClient.RequestedContactIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedContactIds[0], Is.EqualTo(ListingId));
            Assert.That(apiClient.RequestedSimilarIds, Is.Empty);
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenContactFound_ShowsLandlordPhoneAndHidesSignInPrompt()
    {
        var apiClient = new FakeApiClient(
            new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, CreateDetail(DateTime.UtcNow)))
        {
            ContactResult = new ListingContactFetchResult(
                ListingContactFetchOutcome.Found,
                new ListingContactDto("Landlord An", "+84 900 000 000"))
        };
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Content));
            Assert.That(viewModel.LandlordPhone, Is.EqualTo("+84 900 000 000"));
            Assert.That(viewModel.IsPhoneVisible, Is.True);
            Assert.That(viewModel.IsSignInPromptVisible, Is.False);
            Assert.That(apiClient.RequestedContactIds, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task LoadAsync_WhenContactThrows_KeepsContentAndShowsSignInPrompt()
    {
        var apiClient = new FakeApiClient(
            new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, CreateDetail(DateTime.UtcNow)))
        {
            ContactException = new HttpRequestException("network down")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Content));
            Assert.That(viewModel.Listing, Is.Not.Null);
            Assert.That(viewModel.LandlordPhone, Is.Empty);
            Assert.That(viewModel.IsSignInPromptVisible, Is.True);
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task RefreshContactCommand_AfterSignIn_LoadsPhone()
    {
        var apiClient = new FakeApiClient(
            new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, CreateDetail(DateTime.UtcNow)));
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.That(viewModel.IsSignInPromptVisible, Is.True);

        apiClient.ContactResult = new ListingContactFetchResult(
            ListingContactFetchOutcome.Found,
            new ListingContactDto("Landlord An", "+84 900 000 000"));

        await viewModel.RefreshContactCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.LandlordPhone, Is.EqualTo("+84 900 000 000"));
            Assert.That(viewModel.IsPhoneVisible, Is.True);
            Assert.That(viewModel.IsSignInPromptVisible, Is.False);
            Assert.That(apiClient.RequestedContactIds, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task RefreshContactCommand_WhenNoListingLoaded_DoesNotCallApi()
    {
        var apiClient = new FakeApiClient(
            new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null));
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        await viewModel.RefreshContactCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedContactIds, Is.Empty);
            Assert.That(viewModel.LandlordPhone, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenListingUnavailable_ShowsUnavailableStateAndLoadsSimilarRooms()
    {
        var similar = new[]
        {
            CreateSummary(Guid.NewGuid(), "Nearby room"),
            CreateSummary(Guid.NewGuid(), "Corner room")
        };
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.Unavailable, null))
        {
            SimilarResult = similar
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Unavailable));
            Assert.That(viewModel.Listing, Is.Null);
            Assert.That(viewModel.IsUnavailableVisible, Is.True);
            Assert.That(viewModel.IsContentVisible, Is.False);
            Assert.That(viewModel.IsNotFoundVisible, Is.False);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.SimilarRooms.Select(room => room.Id), Is.EqualTo(similar.Select(room => room.Id)));
            Assert.That(viewModel.HasSimilarRooms, Is.True);
            Assert.That(viewModel.IsSimilarLoading, Is.False);
            Assert.That(viewModel.SimilarStatusMessage, Is.Empty);
            Assert.That(viewModel.HasSimilarStatusMessage, Is.False);
            Assert.That(apiClient.RequestedSimilarIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedSimilarIds[0], Is.EqualTo(ListingId));
            Assert.That(apiClient.RequestedSimilarLimits, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedSimilarLimits[0], Is.EqualTo(4));
            Assert.That(apiClient.RequestedContactIds, Is.Empty);
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenUnavailableAndNoSimilarRooms_ShowsEmptyMessage()
    {
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.Unavailable, null));
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Unavailable));
            Assert.That(viewModel.SimilarRooms, Is.Empty);
            Assert.That(viewModel.HasSimilarRooms, Is.False);
            Assert.That(viewModel.SimilarStatusMessage, Is.EqualTo("No similar rooms are available right now."));
            Assert.That(viewModel.HasSimilarStatusMessage, Is.True);
            Assert.That(viewModel.IsSimilarLoading, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WhenSimilarRoomsThrow_StaysUnavailableAndShowsFailureMessage()
    {
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.Unavailable, null))
        {
            SimilarException = new HttpRequestException("network down")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Unavailable));
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.SimilarRooms, Is.Empty);
            Assert.That(viewModel.SimilarStatusMessage, Is.EqualTo("Could not load similar rooms."));
            Assert.That(viewModel.HasSimilarStatusMessage, Is.True);
            Assert.That(viewModel.IsSimilarLoading, Is.False);
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task OpenSimilarRoomCommand_WhenInvoked_NavigatesToThatRoom()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = new RoomDetailViewModel(
            new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.Unavailable, null)),
            new FakeErrorHandler(),
            navigationService);
        var similar = CreateSummary(Guid.NewGuid(), "Nearby room");

        await viewModel.OpenSimilarRoomCommand.ExecuteAsync(similar);

        Assert.Multiple(() =>
        {
            Assert.That(navigationService.RequestedListingIds, Has.Count.EqualTo(1));
            Assert.That(navigationService.RequestedListingIds[0], Is.EqualTo(similar.Id));
        });
    }

    [Test]
    public async Task ShowLoginCommand_WhenInvoked_NavigatesToLogin()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = new RoomDetailViewModel(
            new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null)),
            new FakeErrorHandler(),
            navigationService);

        await viewModel.ShowLoginCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoToLoginCount, Is.EqualTo(1));
    }

    [Test]
    public async Task LoadAsync_WhenListingNotFound_ShowsNotFoundState()
    {
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null));
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.NotFound));
            Assert.That(viewModel.Listing, Is.Null);
            Assert.That(viewModel.IsNotFoundVisible, Is.True);
            Assert.That(viewModel.IsContentVisible, Is.False);
            Assert.That(viewModel.IsUnavailableVisible, Is.False);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedSimilarIds, Is.Empty);
            Assert.That(apiClient.RequestedContactIds, Is.Empty);
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenApiThrows_ShowsErrorStateAndReportsException()
    {
        var exception = new HttpRequestException("network down");
        var apiClient = new FakeApiClient(exception);
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Error));
            Assert.That(viewModel.Listing, Is.Null);
            Assert.That(viewModel.IsErrorVisible, Is.True);
            Assert.That(viewModel.IsContentVisible, Is.False);
            Assert.That(viewModel.IsUnavailableVisible, Is.False);
            Assert.That(viewModel.IsNotFoundVisible, Is.False);
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedContactIds, Is.Empty);
            Assert.That(apiClient.RequestedSimilarIds, Is.Empty);
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public async Task LoadAsync_WhenFoundDetailIsNull_ShowsErrorStateAndReportsException()
    {
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, null));
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Error));
            Assert.That(viewModel.Listing, Is.Null);
            Assert.That(viewModel.IsErrorVisible, Is.True);
            Assert.That(viewModel.IsContentVisible, Is.False);
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task LoadAsync_WhenFirstAttemptFails_RetryLoadsContent()
    {
        var exception = new HttpRequestException("network down");
        var apiClient = new FakeApiClient(exception);
        var errorHandler = new FakeErrorHandler();
        var viewModel = new RoomDetailViewModel(apiClient, errorHandler, new FakeNavigationService());
        ApplyListingId(viewModel, ListingId);

        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Error));
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });

        apiClient.Exception = null;
        apiClient.Result = new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, CreateDetail(DateTime.UtcNow));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Content));
            Assert.That(viewModel.Listing, Is.Not.Null);
            Assert.That(viewModel.Listing?.Id, Is.EqualTo(ListingId));
            Assert.That(viewModel.IsContentVisible, Is.True);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.IsLoadingVisible, Is.False);
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(2));
            Assert.That(apiClient.RequestedIds[1], Is.EqualTo(ListingId));
            Assert.That(apiClient.RequestedContactIds, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task LoadAsync_BeforeQueryAttributes_DoesNotCallApi_ThenApplyLoadsDetails()
    {
        var apiClient = new FakeApiClient(
            new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, CreateDetail(DateTime.UtcNow)));
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        // OnAppearing can fire before Shell applies the query attributes.
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedIds, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Initial));
            Assert.That(viewModel.IsLoadingVisible, Is.True);
            Assert.That(viewModel.Listing, Is.Null);
        });

        ApplyListingId(viewModel, ListingId);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedIds[0], Is.EqualTo(ListingId));
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.Content));
            Assert.That(viewModel.Listing, Is.Not.Null);
            Assert.That(viewModel.Listing?.Id, Is.EqualTo(ListingId));
        });
    }

    [TestCase(null, TestName = "ApplyQueryAttributes_WhenIdMissing_ShowsNotFoundStateAndDoesNotCallApi")]
    [TestCase("", TestName = "ApplyQueryAttributes_WhenIdIsEmpty_ShowsNotFoundStateAndDoesNotCallApi")]
    [TestCase("not-a-guid", TestName = "ApplyQueryAttributes_WhenIdIsInvalid_ShowsNotFoundStateAndDoesNotCallApi")]
    public async Task ApplyQueryAttributes_WhenIdMissingOrInvalid_ShowsNotFoundStateAndDoesNotCallApi(string? idValue)
    {
        var apiClient = new FakeApiClient(
            new ListingDetailFetchResult(ListingDetailFetchOutcome.Found, CreateDetail(DateTime.UtcNow)));
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());
        var query = new Dictionary<string, object>();
        if (idValue is not null)
            query[RoomDetailViewModel.ListingIdQueryKey] = idValue;

        viewModel.ApplyQueryAttributes(query);

        Assert.That(
            viewModel.LoadCommand.ExecutionTask,
            Is.Null,
            "An id that cannot be parsed must not start the load automatically.");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.NotFound));
            Assert.That(viewModel.IsNotFoundVisible, Is.True);
            Assert.That(viewModel.Listing, Is.Null);
            Assert.That(apiClient.RequestedIds, Is.Empty);
        });
    }

    [Test]
    public async Task ApplyQueryAttributes_WhenIdProvided_StoresIdSoLoadUsesIt()
    {
        var listingId = Guid.NewGuid();
        var apiClient = new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null));
        var viewModel = new RoomDetailViewModel(apiClient, new FakeErrorHandler(), new FakeNavigationService());

        ApplyListingId(viewModel, listingId);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedIds, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedIds[0], Is.EqualTo(listingId));
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailState.NotFound));
        });
    }

    [Test]
    public async Task GoBackAsync_WhenInvoked_CallsNavigationService()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = new RoomDetailViewModel(
            new FakeApiClient(new ListingDetailFetchResult(ListingDetailFetchOutcome.NotFound, null)),
            new FakeErrorHandler(),
            navigationService);

        await viewModel.GoBackCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoBackCount, Is.EqualTo(1));
    }

    private static ListingDetailDto CreateDetail(DateTime createdAt)
        => new(
            ListingId,
            "Sunny room",
            "Bright and quiet room near the market.",
            1_500_000,
            3_000_000,
            25,
            RoomType.Apartment,
            1,
            "1 Main St",
            16.0,
            108.0,
            ListingStatus.Published,
            createdAt,
            createdAt.AddDays(2),
            new[] { AmenityCode.Wifi, AmenityCode.AirConditioner },
            new[] { "No smoking", "Quiet hours after 22:00" },
            "Landlord An");

    private static ListingSummaryDto CreateSummary(Guid id, string title)
        => new(
            id,
            title,
            1_600_000,
            26,
            1,
            "2 Side St",
            16.1,
            108.1,
            DateTime.UtcNow,
            null);

    private static void ApplyListingId(RoomDetailViewModel viewModel, Guid listingId)
        => viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [RoomDetailViewModel.ListingIdQueryKey] = listingId.ToString("D")
        });

    // A valid id starts the load inside ApplyQueryAttributes, so the test awaits the auto-started
    // execution instead of invoking the command a second time (which would issue a second call).
    private static Task AwaitAutoStartedLoadAsync(RoomDetailViewModel viewModel)
    {
        var executionTask = viewModel.LoadCommand.ExecutionTask;

        Assert.That(executionTask, Is.Not.Null, "Applying a valid listing id should start the load automatically.");

        return executionTask!;
    }

    private sealed class FakeApiClient : IApiClient
    {
        public FakeApiClient(ListingDetailFetchResult result)
        {
            Result = result;
        }

        public FakeApiClient(Exception exception)
        {
            Exception = exception;
        }

        public List<Guid> RequestedIds { get; } = new();

        public List<Guid> RequestedContactIds { get; } = new();

        public List<Guid> RequestedSimilarIds { get; } = new();

        public List<int> RequestedSimilarLimits { get; } = new();

        public ListingDetailFetchResult? Result { get; set; }

        public Exception? Exception { get; set; }

        public ListingContactFetchResult ContactResult { get; set; } =
            new(ListingContactFetchOutcome.Unauthorized, null);

        public Exception? ContactException { get; set; }

        public IReadOnlyList<ListingSummaryDto> SimilarResult { get; set; } = Array.Empty<ListingSummaryDto>();

        public Exception? SimilarException { get; set; }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The room detail screen does not load listing summaries.");

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            RequestedIds.Add(id);

            return Exception is { } exception
                ? Task.FromException<ListingDetailFetchResult>(exception)
                : Task.FromResult(Result!);
        }

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
        {
            RequestedSimilarIds.Add(id);
            RequestedSimilarLimits.Add(limit);

            return SimilarException is { } exception
                ? Task.FromException<IReadOnlyList<ListingSummaryDto>>(exception)
                : Task.FromResult(SimilarResult);
        }

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            RequestedContactIds.Add(id);

            return ContactException is { } exception
                ? Task.FromException<ListingContactFetchResult>(exception)
                : Task.FromResult(ContactResult);
        }

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The room detail screen does not sign in.");

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The room detail screen does not sign in.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The room detail screen does not sign out.");
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = new();

        public void HandleError(Exception ex)
        {
            HandledExceptions.Add(ex);
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<Guid> RequestedListingIds { get; } = new();

        public int GoBackCount { get; private set; }

        public int GoToLoginCount { get; private set; }

        public Task GoToRoomDetailAsync(Guid listingId)
        {
            RequestedListingIds.Add(listingId);
            return Task.CompletedTask;
        }

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoBackAsync()
        {
            GoBackCount++;
            return Task.CompletedTask;
        }
    }
}
