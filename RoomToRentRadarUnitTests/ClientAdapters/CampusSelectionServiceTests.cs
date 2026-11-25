using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Mocks.SampleData;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadarUnitTests.ClientAdapters;

/// <summary>T13.4 — BR-CMP-01..03, UC-02 A1, UC-24.</summary>
[TestFixture]
public class CampusSelectionServiceTests
{
    private ICampusGateway _campusGateway = null!;
    private IProfileGateway _profileGateway = null!;
    private IAuthGateway _authGateway = null!;
    private ISessionStore _sessionStore = null!;
    private InMemoryPreferencesStore _preferences = null!;
    private int _changedCount;

    [SetUp]
    public void SetUp()
    {
        _campusGateway = Substitute.For<ICampusGateway>();
        _campusGateway.ListCampusesAsync(Arg.Any<CancellationToken>()).Returns(SampleCampuses.All);
        _profileGateway = Substitute.For<IProfileGateway>();
        _authGateway = Substitute.For<IAuthGateway>();
        _sessionStore = Substitute.For<ISessionStore>();
        _preferences = new InMemoryPreferencesStore();
        _changedCount = 0;
    }

    private CampusSelectionService CreateService()
    {
        var service = new CampusSelectionService(
            _campusGateway, _profileGateway, _authGateway, _sessionStore, _preferences);
        service.SelectedCampusChanged += (_, _) => _changedCount++;
        return service;
    }

    private void SignIn() =>
        _sessionStore.HasValidTokenAsync(Arg.Any<CancellationToken>()).Returns(true);

    private void ProfileHasCampus(string campusId) =>
        _authGateway.GetCurrentUserProfileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(
            new UserProfileDto(
                "u1", "student@ictu.edu.vn", "Student", "Student", "Local", null, null, true,
                DateTime.UtcNow, SelectedCampusId: campusId, PreferredLanguage: "en"));

    private void StoreDevice(string campusId, bool pending)
    {
        _preferences.SetString(CampusSelectionService.SelectedCampusKey, campusId);
        if (pending)
        {
            _preferences.SetString(CampusSelectionService.PendingUploadKey, "1");
        }
    }

    private string? Stored => _preferences.GetString(CampusSelectionService.SelectedCampusKey);

    private bool Pending => _preferences.GetString(CampusSelectionService.PendingUploadKey) is not null;

    private Task AssertUploadedAsync(string campusId) =>
        _profileGateway.Received(1).UpdateMyProfileAsync(
            new UpdateMyProfileRequest(null, null, campusId, null), Arg.Any<CancellationToken>());

    private Task AssertNotUploadedAsync() =>
        _profileGateway.DidNotReceive().UpdateMyProfileAsync(
            Arg.Any<UpdateMyProfileRequest>(), Arg.Any<CancellationToken>());

    [Test]
    public void DefaultsToIctuWhenNothingStored()
    {
        Assert.That(CreateService().SelectedCampusId, Is.EqualTo("ICTU"));
    }

    [Test]
    public void LoadsStoredPreference()
    {
        StoreDevice("TNUT", pending: false);

        Assert.That(CreateService().SelectedCampusId, Is.EqualTo("TNUT"));
    }

    [Test]
    public async Task Guest_Select_StoresLocally_RaisesEventOnce_NoGatewayCall()
    {
        var service = CreateService();

        await service.SelectCampusAsync("TNUT");

        Assert.Multiple(() =>
        {
            Assert.That(service.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(Stored, Is.EqualTo("TNUT"));
            Assert.That(Pending, Is.True);
            Assert.That(_changedCount, Is.EqualTo(1));
        });
        await AssertNotUploadedAsync();
    }

    [Test]
    public async Task SignedIn_Select_CallsUpdateMyProfileWithOnlyCampus_ThenStoresAndRaises()
    {
        SignIn();
        var service = CreateService();

        await service.SelectCampusAsync("TUMP");

        await AssertUploadedAsync("TUMP");
        Assert.Multiple(() =>
        {
            Assert.That(Stored, Is.EqualTo("TUMP"));
            Assert.That(Pending, Is.False);
            Assert.That(_changedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SignedIn_GatewayThrows_NoStoreNoEvent_ExceptionPropagates()
    {
        SignIn();
        _profileGateway.UpdateMyProfileAsync(Arg.Any<UpdateMyProfileRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException("offline", 0, ApiErrorCodes.NetworkError));
        var service = CreateService();

        Assert.ThrowsAsync<ApiException>(async () => await service.SelectCampusAsync("TUMP"));

        Assert.Multiple(() =>
        {
            Assert.That(service.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(Stored, Is.Null);
            Assert.That(_changedCount, Is.Zero);
        });

        await Task.CompletedTask;
    }

    [Test]
    public async Task Select_SameCampus_NoCallNoEvent()
    {
        SignIn();
        var service = CreateService();

        await service.SelectCampusAsync("ICTU");

        Assert.That(_changedCount, Is.Zero);
        await AssertNotUploadedAsync();
    }

    [Test]
    public async Task Select_UnknownIdWhenListLoaded_Throws()
    {
        var service = CreateService();
        await service.GetCampusesAsync();

        Assert.ThrowsAsync<ArgumentException>(async () => await service.SelectCampusAsync("NOPE"));
        Assert.That(service.SelectedCampusId, Is.EqualTo("ICTU"));
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task Select_BlankId_Throws(string id)
    {
        var service = CreateService();

        Assert.ThrowsAsync<ArgumentException>(async () => await service.SelectCampusAsync(id));

        await Task.CompletedTask;
    }

    [Test]
    public async Task GetCampuses_CachesAfterSuccess_ForceRefreshRefetches()
    {
        var service = CreateService();

        await service.GetCampusesAsync();
        await service.GetCampusesAsync();
        await _campusGateway.Received(1).ListCampusesAsync(Arg.Any<CancellationToken>());

        await service.GetCampusesAsync(forceRefresh: true);
        await _campusGateway.Received(2).ListCampusesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetCampuses_Failure_NotCached_NextCallRetries()
    {
        _campusGateway.ListCampusesAsync(Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException<IReadOnlyList<CampusDto>>(new ApiException("offline", 0)),
            _ => Task.FromResult(SampleCampuses.All));
        var service = CreateService();

        Assert.ThrowsAsync<ApiException>(async () => await service.GetCampusesAsync());
        var campuses = await service.GetCampusesAsync();

        Assert.That(campuses, Has.Count.EqualTo(SampleCampuses.All.Count));
    }

    [Test]
    public async Task GetCampuses_EmptyAnswer_NotCached()
    {
        _campusGateway.ListCampusesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CampusDto>());
        var service = CreateService();

        await service.GetCampusesAsync();
        await service.GetCampusesAsync();

        await _campusGateway.Received(2).ListCampusesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectedCampus_NullUntilListLoaded_ThenMatchesSelection()
    {
        StoreDevice("TNUT", pending: false);
        var service = CreateService();
        Assert.That(service.SelectedCampus, Is.Null);

        await service.GetCampusesAsync();

        Assert.That(service.SelectedCampus!.ShortName, Is.EqualTo("TNUT"));
    }

    [Test]
    public async Task Sync_Guest_DoesNotReadProfile()
    {
        var service = CreateService();

        await service.SynchronizeAsync();

        await _authGateway.DidNotReceive().GetCurrentUserProfileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [TestCase("ICTU")]
    [TestCase("TUMP")]
    public async Task Sync_NoPendingChoice_ProfileWins(string remote)
    {
        // A choice already saved to a profile must not override a newer one made on another device.
        SignIn();
        StoreDevice("TNUT", pending: false);
        ProfileHasCampus(remote);
        var service = CreateService();

        await service.SynchronizeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.SelectedCampusId, Is.EqualTo(remote));
            Assert.That(Stored, Is.EqualTo(remote));
            Assert.That(_changedCount, Is.EqualTo(1));
        });
        await AssertNotUploadedAsync();
    }

    [Test]
    public async Task Sync_ProfileDefaultPendingGuestChoice_UploadsDeviceChoice()
    {
        SignIn();
        StoreDevice("TNUT", pending: true);
        ProfileHasCampus("ICTU");
        var service = CreateService();

        await service.SynchronizeAsync();

        await AssertUploadedAsync("TNUT");
        Assert.Multiple(() =>
        {
            Assert.That(service.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(Pending, Is.False);
            Assert.That(_changedCount, Is.Zero);
        });
    }

    [Test]
    public async Task Sync_ProfileNonDefaultPendingGuestChoice_ProfileWins()
    {
        SignIn();
        StoreDevice("TNUT", pending: true);
        ProfileHasCampus("TUMP");
        var service = CreateService();

        await service.SynchronizeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.SelectedCampusId, Is.EqualTo("TUMP"));
            Assert.That(Pending, Is.False);
        });
        await AssertNotUploadedAsync();
    }

    [Test]
    public async Task Sync_UploadFails_KeepsDeviceChoicePending_NoThrow()
    {
        SignIn();
        StoreDevice("TNUT", pending: true);
        ProfileHasCampus("ICTU");
        _profileGateway.UpdateMyProfileAsync(Arg.Any<UpdateMyProfileRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException("offline", 0));
        var service = CreateService();

        await service.SynchronizeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(Pending, Is.True);
            Assert.That(_changedCount, Is.Zero);
        });
    }

    [Test]
    public async Task Sync_ProfileReadFails_KeepsDeviceChoice_NoThrow()
    {
        SignIn();
        StoreDevice("TNUT", pending: false);
        _authGateway.GetCurrentUserProfileAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException("offline", 0));
        var service = CreateService();

        await service.SynchronizeAsync();

        Assert.That(service.SelectedCampusId, Is.EqualTo("TNUT"));
    }

    [Test]
    public async Task Sync_Equal_DoesNothing_ClearsPending()
    {
        SignIn();
        StoreDevice("TNUT", pending: true);
        ProfileHasCampus("TNUT");
        var service = CreateService();

        await service.SynchronizeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Pending, Is.False);
            Assert.That(_changedCount, Is.Zero);
        });
        await AssertNotUploadedAsync();
    }

    [Test]
    public async Task ConcurrentSelects_AreSerialized_LastWins()
    {
        SignIn();
        var firstUpload = new TaskCompletionSource<UserProfileDto>();
        _profileGateway.UpdateMyProfileAsync(Arg.Any<UpdateMyProfileRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => firstUpload.Task, _ => Task.FromResult<UserProfileDto>(null!));
        var service = CreateService();

        var first = service.SelectCampusAsync("TNUT");
        var second = service.SelectCampusAsync("TUMP");
        await _profileGateway.Received(1).UpdateMyProfileAsync(
            Arg.Any<UpdateMyProfileRequest>(), Arg.Any<CancellationToken>());

        firstUpload.SetResult(null!);
        await Task.WhenAll(first, second);

        Assert.That(service.SelectedCampusId, Is.EqualTo("TUMP"));
    }
}
