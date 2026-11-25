using NSubstitute;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.Search;

/// <summary>T13.4 AC-8 — the selected campus drives the shared search criteria (BR-CMP-03).</summary>
[TestFixture]
public class CampusSearchBridgeTests
{
    private SearchCriteriaStore _store = null!;
    private ICampusSelectionService _selection = null!;
    private IAppSession _appSession = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new SearchCriteriaStore();
        _selection = Substitute.For<ICampusSelectionService>();
        _selection.SelectedCampusId.Returns("ICTU");
        _appSession = Substitute.For<IAppSession>();
    }

    private CampusSearchBridge CreateBridge() => new(_selection, _store, _appSession);

    private void ChangeSelection(string campusId)
    {
        _selection.SelectedCampusId.Returns(campusId);
        _selection.SelectedCampusChanged += Raise.Event();
    }

    [Test]
    public void Constructed_AppliesStoredCampusToCriteria()
    {
        _selection.SelectedCampusId.Returns("TNUT");

        CreateBridge();

        Assert.Multiple(() =>
        {
            Assert.That(_store.Current.CampusId, Is.EqualTo("TNUT"));
            Assert.That(_store.Current.CampusDisplayName, Is.EqualTo("TNUT"));
        });
    }

    [Test]
    public void CampusChanged_UpdatesCriteria_KeepsFilters()
    {
        CreateBridge();
        _store.Apply(_store.Current with { MaxRentVnd = 2_000_000, RoomTypes = new[] { RoomType.Dormitory } });

        ChangeSelection("TUMP");

        Assert.Multiple(() =>
        {
            Assert.That(_store.Current.CampusId, Is.EqualTo("TUMP"));
            Assert.That(_store.Current.MaxRentVnd, Is.EqualTo(2_000_000));
            Assert.That(_store.Current.RoomTypes, Is.EqualTo(new[] { RoomType.Dormitory }));
        });
    }

    [Test]
    public void ClearFilters_KeepsTheSelectedCampus()
    {
        CreateBridge();
        ChangeSelection("TNUE");

        _store.ClearFilters();

        Assert.That(_store.Current.CampusId, Is.EqualTo("TNUE"));
    }

    [Test]
    public void CampusChanged_ReloadsExploreWithNewCampus()
    {
        var gateway = Substitute.For<IRoomGateway>();
        gateway.SearchRoomsAsync(Arg.Any<RoomSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<RoomCardDto>([], 0, 1, 20));
        CreateBridge();
        var explore = new MainLandingPageModel(
            gateway, Substitute.For<ISessionStore>(), Substitute.For<INavigationService>(), _store);

        ChangeSelection("TNUT");

        gateway.Received(1).SearchRoomsAsync(
            Arg.Is<RoomSearchRequest>(r => r.CampusId == "TNUT"), Arg.Any<CancellationToken>());
        GC.KeepAlive(explore);
    }

    [Test]
    public async Task GuestSelection_ReachesTheSearchCriteria_WithTheRealService()
    {
        var service = new CampusSelectionService(
            new MockCampusGateway(),
            Substitute.For<IProfileGateway>(),
            Substitute.For<IAuthGateway>(),
            Substitute.For<ISessionStore>(),
            new InMemoryPreferencesStore());
        _ = new CampusSearchBridge(service, _store, _appSession);
        await service.GetCampusesAsync();

        await service.SelectCampusAsync("SFL");

        Assert.That(_store.Current.CampusId, Is.EqualTo("SFL"));
    }

    [Test]
    public void SignedInRole_TriggersSynchronize()
    {
        CreateBridge();
        _appSession.CurrentRole.Returns("Student");

        _appSession.RoleChanged += Raise.Event();

        _selection.Received(1).SynchronizeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void GuestRole_DoesNotSynchronize()
    {
        CreateBridge();
        _appSession.CurrentRole.Returns((string?)null);

        _appSession.RoleChanged += Raise.Event();

        _selection.DidNotReceive().SynchronizeAsync(Arg.Any<CancellationToken>());
    }
}
