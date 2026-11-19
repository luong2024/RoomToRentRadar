using Microsoft.Maui.Storage;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.RoomDetail.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomDetailTravelViewModelTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");
    private const double DestinationLat = 10.703;
    private const double DestinationLng = 106.703;

    [Test]
    public async Task Initialize_WithoutStoredCampus_ShowsNoCampusStateAndDoesNotCallApi()
    {
        var client = new FakeTravelEstimatesClient();
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore());

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.NoCampus));
            Assert.That(viewModel.IsNoCampusVisible, Is.True);
            Assert.That(viewModel.Estimates, Is.Empty);
            Assert.That(viewModel.SelectedCampusName, Is.Null);
            Assert.That(client.Calls, Is.Empty);
        });
    }

    [Test]
    public async Task Initialize_WithStoredCampus_LoadsEstimatesForThatCampus()
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = FoundResult(
                new TravelEstimateDto(TravelMode.Motorbike, TravelSource.Route, 6200, 1080),
                new TravelEstimateDto(TravelMode.Bus, TravelSource.Fallback, 4100, null),
                new TravelEstimateDto(TravelMode.Walking, TravelSource.Route, 5000, 3600))
        };
        var store = new FakeCampusSelectionStore { SelectedCampus = "Bách Khoa" };
        var viewModel = CreateViewModel(client, store);

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Loaded));
            Assert.That(viewModel.IsLoadedVisible, Is.True);
            Assert.That(viewModel.SelectedCampusName, Is.EqualTo("Bách Khoa"));
            Assert.That(client.Calls, Has.Count.EqualTo(1));
            Assert.That(client.Calls[0].ListingId, Is.EqualTo(ListingId));
            Assert.That(client.Calls[0].CampusId, Is.EqualTo("bach-khoa"));
            Assert.That(
                viewModel.Estimates.Select(item => item.ModeLabel),
                Is.EqualTo(new[] { "Xe máy", "Xe buýt", "Đi bộ" }));
            Assert.That(viewModel.Estimates[0].ValueText, Is.EqualTo("18 phút · 6,2 km"));
            Assert.That(viewModel.Estimates[0].IsFallback, Is.False);
            Assert.That(viewModel.Estimates[1].ValueText, Is.EqualTo("≈ 4,1 km đường thẳng"));
            Assert.That(viewModel.Estimates[1].IsFallback, Is.True);
            Assert.That(viewModel.Estimates[2].ValueText, Is.EqualTo("1 giờ · 5 km"));
            Assert.That(viewModel.HasFallbackNote, Is.True);
        });
    }

    [Test]
    public async Task SelectingCampus_PersistsChoiceAndLoadsEstimates()
    {
        var client = new FakeTravelEstimatesClient { Result = FoundResult(RouteEstimate()) };
        var store = new FakeCampusSelectionStore();
        var viewModel = CreateViewModel(client, store);
        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        viewModel.SelectedCampusName = "KHTN";
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(store.SelectedCampus, Is.EqualTo("KHTN"));
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Loaded));
            Assert.That(client.Calls, Has.Count.EqualTo(1));
            Assert.That(client.Calls[0].CampusId, Is.EqualTo("khtn"));
        });
    }

    [Test]
    public async Task LoadAsync_WhenApiFails_ShowsErrorWithoutFabricatedDistance()
    {
        var client = new FakeTravelEstimatesClient { Exception = new HttpRequestException("network down") };
        var store = new FakeCampusSelectionStore { SelectedCampus = "KHTN" };
        var viewModel = CreateViewModel(client, store);

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Error));
            Assert.That(viewModel.IsErrorVisible, Is.True);
            Assert.That(viewModel.Estimates, Is.Empty);
        });

        client.Exception = null;
        client.Result = FoundResult(RouteEstimate());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Loaded));
            Assert.That(viewModel.Estimates, Has.Count.EqualTo(1));
            Assert.That(client.Calls, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task LoadAsync_WhenListingIsGone_ShowsListingGoneState()
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.NotFound, null)
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.ListingGone));
            Assert.That(viewModel.IsListingGoneVisible, Is.True);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.Estimates, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenRateLimited_ShowsRetryMessageWithDelay()
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = new TravelEstimatesFetchResult(
                TravelEstimatesFetchOutcome.RateLimited,
                null,
                TimeSpan.FromSeconds(30))
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.RateLimited));
            Assert.That(viewModel.IsRateLimitedVisible, Is.True);
            Assert.That(viewModel.IsErrorVisible, Is.False);
            Assert.That(viewModel.Estimates, Is.Empty);
            Assert.That(viewModel.RateLimitedMessage, Does.Contain("30 giây"));
        });
    }

    [Test]
    public async Task LoadAsync_WhenRateLimitedWithoutRetryAfter_UsesGenericMessage()
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.RateLimited, null)
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.RateLimited));
            Assert.That(viewModel.RateLimitedMessage, Does.Contain("ít phút"));
        });
    }

    [Test]
    public async Task Initialize_SameListingAfterError_RetriesTheLoad()
    {
        var client = new FakeTravelEstimatesClient { Exception = new HttpRequestException("network down") };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);
        Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Error));

        client.Exception = null;
        client.Result = FoundResult(RouteEstimate());

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(2));
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Loaded));
        });
    }

    [Test]
    public async Task ChangingCampus_CancelsThePreviousRequest()
    {
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeTravelEstimatesClient
        {
            Handler = async (_, campusId, cancellationToken) =>
            {
                if (campusId == "bach-khoa")
                {
                    firstRequestStarted.TrySetResult();
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return FoundResult(RouteEstimate());
            }
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "Bách Khoa" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        var firstExecution = viewModel.LoadCommand.ExecutionTask;
        Assert.That(firstExecution, Is.Not.Null);
        await firstRequestStarted.Task;

        viewModel.SelectedCampusName = "KHTN";
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Loaded));
            Assert.That(client.Calls, Has.Count.EqualTo(2));
            Assert.That(client.Calls[0].CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(client.Calls[1].CancellationToken.IsCancellationRequested, Is.False);
            Assert.That(client.Calls[1].CampusId, Is.EqualTo("khtn"));
        });
    }

    [Test]
    public async Task Cancel_StopsTheInFlightRequest()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeTravelEstimatesClient
        {
            Handler = async (_, _, cancellationToken) =>
            {
                requestStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return FoundResult(RouteEstimate());
            }
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await requestStarted.Task;

        viewModel.Cancel();

        Assert.That(client.Calls[0].CancellationToken.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task Initialize_SameListingAfterLoad_DoesNotRequestAgain()
    {
        var client = new FakeTravelEstimatesClient { Result = FoundResult(RouteEstimate()) };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);
        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await Task.Yield();

        Assert.That(client.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Initialize_NewListing_ReplacesTheEstimates()
    {
        var client = new FakeTravelEstimatesClient { Result = FoundResult(RouteEstimate()) };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        var otherListingId = Guid.NewGuid();
        viewModel.Initialize(otherListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(2));
            Assert.That(client.Calls[1].ListingId, Is.EqualTo(otherListingId));
            Assert.That(viewModel.State, Is.EqualTo(RoomDetailTravelState.Loaded));
        });
    }

    [TestCase(1080, "18 phút")]
    [TestCase(30, "1 phút")]
    [TestCase(3540, "59 phút")]
    [TestCase(3600, "1 giờ")]
    [TestCase(5400, "1 giờ 30 phút")]
    [TestCase(7260, "2 giờ 1 phút")]
    public async Task LoadedState_FormatsDurationsInVietnamese(int durationSeconds, string expected)
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = FoundResult(
                new TravelEstimateDto(TravelMode.Motorbike, TravelSource.Route, 6200, durationSeconds))
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.That(viewModel.Estimates[0].ValueText, Is.EqualTo($"{expected} · 6,2 km"));
    }

    [TestCase(850, "850 m")]
    [TestCase(1000, "1 km")]
    [TestCase(1250, "1,3 km")]
    [TestCase(12345, "12,3 km")]
    public async Task LoadedState_FormatsFallbackDistancesInVietnamese(int distanceM, string expected)
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = FoundResult(
                new TravelEstimateDto(TravelMode.Walking, TravelSource.Fallback, distanceM, null))
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Estimates[0].ValueText, Is.EqualTo($"≈ {expected} đường thẳng"));
            Assert.That(viewModel.HasFallbackNote, Is.True);
        });
    }

    [Test]
    public async Task Estimates_WithoutFallback_HideTheExplanationNote()
    {
        var client = new FakeTravelEstimatesClient
        {
            Result = FoundResult(
                new TravelEstimateDto(TravelMode.Motorbike, TravelSource.Route, 6200, 900),
                new TravelEstimateDto(TravelMode.Bus, TravelSource.Route, 7000, 1200))
        };
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore { SelectedCampus = "KHTN" });

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasFallbackNote, Is.False);
            Assert.That(viewModel.Estimates.Select(item => item.AccessibilityText),
                Is.EqualTo(new[] { "Xe máy: 15 phút · 6,2 km", "Xe buýt: 20 phút · 7 km" }));
        });
    }

    [Test]
    public async Task OpenDirections_OpensInAppDirectionsFromSelectedCampus()
    {
        var client = new FakeTravelEstimatesClient { Result = FoundResult(RouteEstimate()) };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            client,
            new FakeCampusSelectionStore { SelectedCampus = "KHTN" },
            navigation);

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        await viewModel.OpenDirectionsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigation.Directions, Has.Count.EqualTo(1));
            Assert.That(navigation.Directions[0].DestinationLat, Is.EqualTo(DestinationLat));
            Assert.That(navigation.Directions[0].DestinationLng, Is.EqualTo(DestinationLng));
            Assert.That(navigation.Directions[0].OriginLat, Is.EqualTo(10.7626));
            Assert.That(navigation.Directions[0].OriginLng, Is.EqualTo(106.6822));
        });
    }

    [Test]
    public async Task OpenDirections_WithoutSelectedCampus_OmitsTheOrigin()
    {
        var client = new FakeTravelEstimatesClient { Result = FoundResult(RouteEstimate()) };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(client, new FakeCampusSelectionStore(), navigation);

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        await viewModel.OpenDirectionsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigation.Directions, Has.Count.EqualTo(1));
            Assert.That(navigation.Directions[0].OriginLat, Is.Null);
            Assert.That(navigation.Directions[0].OriginLng, Is.Null);
        });
    }

    [Test]
    public async Task OpenDirections_WhenNavigationFails_NotifiesTheUser()
    {
        var client = new FakeTravelEstimatesClient { Result = FoundResult(RouteEstimate()) };
        var navigation = new FakeNavigationService
        {
            Exception = new InvalidOperationException("shell not ready")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            client,
            new FakeCampusSelectionStore { SelectedCampus = "KHTN" },
            navigation,
            errorHandler);

        viewModel.Initialize(ListingId, DestinationLat, DestinationLng);
        await AwaitAutoStartedLoadAsync(viewModel);

        await viewModel.OpenDirectionsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigation.Directions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    private static RoomDetailTravelViewModel CreateViewModel(
        FakeTravelEstimatesClient client,
        FakeCampusSelectionStore store,
        FakeNavigationService? navigation = null,
        FakeErrorHandler? errorHandler = null)
        => new(
            client,
            store,
            navigation ?? new FakeNavigationService(),
            errorHandler ?? new FakeErrorHandler());

    private static TravelEstimateDto RouteEstimate()
        => new(TravelMode.Motorbike, TravelSource.Route, 6200, 1080);

    private static TravelEstimatesFetchResult FoundResult(params TravelEstimateDto[] modes)
        => new(TravelEstimatesFetchOutcome.Found, new ListingTravelEstimatesDto(modes));

    // A valid campus starts the load inside Initialize/selection, so the test awaits
    // the auto-started execution instead of invoking the command a second time.
    private static Task AwaitAutoStartedLoadAsync(RoomDetailTravelViewModel viewModel)
    {
        var executionTask = viewModel.LoadCommand.ExecutionTask;

        Assert.That(executionTask, Is.Not.Null, "A selected campus should start the load automatically.");

        return executionTask!;
    }

    private sealed class FakeTravelEstimatesClient : ITravelEstimatesClient
    {
        public Func<Guid, string, CancellationToken, Task<TravelEstimatesFetchResult>>? Handler { get; set; }

        public TravelEstimatesFetchResult? Result { get; set; }

        public Exception? Exception { get; set; }

        public List<Call> Calls { get; } = [];

        public Task<TravelEstimatesFetchResult> GetEstimatesAsync(
            Guid listingId,
            string campusId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new Call(listingId, campusId, cancellationToken));

            if (Handler is { } handler)
            {
                return handler(listingId, campusId, cancellationToken);
            }

            return Exception is { } exception
                ? Task.FromException<TravelEstimatesFetchResult>(exception)
                : Task.FromResult(Result!);
        }

        public sealed record Call(Guid ListingId, string CampusId, CancellationToken CancellationToken);
    }

    private sealed class FakeCampusSelectionStore : ICampusSelectionStore
    {
        public string? SelectedCampus { get; set; }

        public string? GetSelectedCampus() => SelectedCampus;

        public void SetSelectedCampus(string? campusName) => SelectedCampus = campusName;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Exception? Exception { get; set; }

        public List<DirectionsCall> Directions { get; } = [];

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
        {
            Directions.Add(new DirectionsCall(destinationLat, destinationLng, originLat, originLng));

            return Exception is { } exception
                ? Task.FromException(exception)
                : Task.CompletedTask;
        }

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The travel section does not open room details.");

        public Task GoToLoginAsync()
            => throw new NotSupportedException("The travel section does not open the login screen.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The travel section does not navigate back.");

        public sealed record DirectionsCall(
            double DestinationLat,
            double DestinationLng,
            double? OriginLat,
            double? OriginLng);
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> Errors { get; } = [];

        public void HandleError(Exception ex) => Errors.Add(ex);
    }
}

[TestFixture]
public class CampusSelectionStoreTests
{
    [Test]
    public void SetSelectedCampus_ThenGetSelectedCampus_ReturnsStoredCampus()
    {
        var store = new PreferencesCampusSelectionStore(new FakePreferences());

        store.SetSelectedCampus("Bách Khoa");

        Assert.That(store.GetSelectedCampus(), Is.EqualTo("Bách Khoa"));
    }

    [Test]
    public void SetSelectedCampus_TrimsWhitespace()
    {
        var store = new PreferencesCampusSelectionStore(new FakePreferences());

        store.SetSelectedCampus("  KHTN  ");

        Assert.That(store.GetSelectedCampus(), Is.EqualTo("KHTN"));
    }

    [Test]
    public void SetSelectedCampus_NullOrBlank_ClearsTheStoredValue()
    {
        var preferences = new FakePreferences();
        var store = new PreferencesCampusSelectionStore(preferences);
        store.SetSelectedCampus("KHTN");

        store.SetSelectedCampus(null);

        Assert.Multiple(() =>
        {
            Assert.That(store.GetSelectedCampus(), Is.Null);
            Assert.That(preferences.ContainsKey(PreferencesCampusSelectionStore.PreferencesKey), Is.False);
        });
    }

    [Test]
    public void GetSelectedCampus_WithoutStoredValue_ReturnsNull()
    {
        var store = new PreferencesCampusSelectionStore(new FakePreferences());

        Assert.That(store.GetSelectedCampus(), Is.Null);
    }

    private sealed class FakePreferences : IPreferences
    {
        private readonly Dictionary<string, object?> _values = [];

        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);

        public void Remove(string key, string? sharedName = null) => _values.Remove(key);

        public void Clear(string? sharedName = null) => _values.Clear();

        public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

        public T Get<T>(string key, T defaultValue, string? sharedName = null)
            => _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
    }
}

[TestFixture]
public class DirectionsUrlBuilderTests
{
    [Test]
    public void Build_WithOriginAndDestination_UsesInvariantCoordinates()
    {
        var url = DirectionsUrlBuilder.Build(10.703, 106.703, 10.7626, 106.6822);

        Assert.That(
            url,
            Is.EqualTo(
                "https://www.google.com/maps/dir/?api=1&origin=10.7626,106.6822&destination=10.703,106.703"));
    }

    [Test]
    public void Build_WithoutOrigin_OnlyIncludesTheDestination()
    {
        var url = DirectionsUrlBuilder.Build(10.5, 106.25);

        Assert.That(
            url,
            Is.EqualTo("https://www.google.com/maps/dir/?api=1&destination=10.5,106.25"));
    }
}
