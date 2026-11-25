using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>
/// The campus distances are measured from (BR-CMP-01..03). A guest's choice stays on the device;
/// a signed-in user's choice is saved to the profile and shared across devices (BR-CMP-02, UC-24).
/// </summary>
public interface ICampusSelectionService
{
    /// <summary>BR-CMP-01: the campus used until the user picks another one.</summary>
    public const string DefaultCampusId = "ICTU";

    /// <summary>Always has a value; <see cref="DefaultCampusId"/> until something else is chosen.</summary>
    string SelectedCampusId { get; }

    /// <summary><c>null</c> until the campus list has been loaded.</summary>
    CampusDto? SelectedCampus { get; }

    /// <summary>Raised after <see cref="SelectedCampusId"/> changes, on the caller's synchronization context.</summary>
    event EventHandler? SelectedCampusChanged;

    /// <summary>The last non-empty campus list, or <c>null</c> if none has been loaded yet.</summary>
    IReadOnlyList<CampusDto>? CachedCampuses { get; }

    /// <summary>The campus list, cached in memory after the first non-empty answer; failures are not cached.</summary>
    Task<IReadOnlyList<CampusDto>> GetCampusesAsync(bool forceRefresh = false, CancellationToken ct = default);

    /// <summary>
    /// Selects a campus. When signed in the profile is saved first: if that fails the exception
    /// propagates and the selection stays unchanged.
    /// </summary>
    Task SelectCampusAsync(string campusId, CancellationToken ct = default);

    /// <summary>
    /// UC-02 A1: reconciles the device choice with the server profile. A choice made while signed out is
    /// uploaded if the profile still has the default campus; otherwise the profile wins. Does nothing for
    /// a guest and never throws on server or network errors.
    /// </summary>
    Task SynchronizeAsync(CancellationToken ct = default);
}
