using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.Services.Search.Core;

/// <summary>
/// Connects the selected campus (T13.4) to the shared search criteria Explore reads (T04.2), so results
/// follow the campus (BR-CMP-03), and reconciles it with the profile when a session starts (UC-02 A1).
/// All three collaborators are app-wide singletons; resolve this once at start-up.
/// </summary>
public sealed class CampusSearchBridge
{
    private readonly ICampusSelectionService _selection;
    private readonly ISearchCriteriaStore _criteriaStore;
    private readonly IAppSession _appSession;

    public CampusSearchBridge(
        ICampusSelectionService selection,
        ISearchCriteriaStore criteriaStore,
        IAppSession appSession)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _criteriaStore = criteriaStore ?? throw new ArgumentNullException(nameof(criteriaStore));
        _appSession = appSession ?? throw new ArgumentNullException(nameof(appSession));

        _selection.SelectedCampusChanged += (_, _) => ApplySelection();
        _appSession.RoleChanged += OnRoleChanged;
        ApplySelection();
    }

    private void ApplySelection()
    {
        var campusId = _selection.SelectedCampusId;
        _criteriaStore.SetCampus(campusId, _selection.SelectedCampus?.ShortName ?? campusId);
    }

    // A role appears at start-up with a stored session and right after sign-in.
    private void OnRoleChanged(object? sender, EventArgs e)
    {
        if (_appSession.CurrentRole is not null)
        {
            // SynchronizeAsync reports its own failures; nothing here waits for it.
            _ = _selection.SynchronizeAsync();
        }
    }
}
