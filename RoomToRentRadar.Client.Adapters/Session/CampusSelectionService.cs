using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Session;

/// <summary>
/// Default <see cref="ICampusSelectionService"/>. Selection changes and synchronisation are serialised so
/// a slow profile read can never overwrite a newer choice. Awaits deliberately keep the caller's context:
/// <see cref="SelectedCampusChanged"/> subscribers update UI-bound state.
/// </summary>
public sealed class CampusSelectionService : ICampusSelectionService
{
    /// <summary>erd §5 device preference key.</summary>
    public const string SelectedCampusKey = "SelectedCampusId";

    /// <summary>Set while the device choice was made signed out and has not reached a profile yet (UC-02 A1).</summary>
    public const string PendingUploadKey = "SelectedCampusPendingUpload";

    private const string DefaultCampusId = ICampusSelectionService.DefaultCampusId;
    private const string PendingValue = "1";

    private readonly ICampusGateway _campusGateway;
    private readonly IProfileGateway _profileGateway;
    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;
    private readonly IPreferencesStore _preferences;
    private readonly ILogger<CampusSelectionService>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<CampusDto>? _campuses;
    private string _selectedCampusId;

    public CampusSelectionService(
        ICampusGateway campusGateway,
        IProfileGateway profileGateway,
        IAuthGateway authGateway,
        ISessionStore sessionStore,
        IPreferencesStore preferences,
        ILogger<CampusSelectionService>? logger = null)
    {
        _campusGateway = campusGateway ?? throw new ArgumentNullException(nameof(campusGateway));
        _profileGateway = profileGateway ?? throw new ArgumentNullException(nameof(profileGateway));
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _logger = logger;

        var stored = _preferences.GetString(SelectedCampusKey);
        _selectedCampusId = string.IsNullOrWhiteSpace(stored) ? DefaultCampusId : stored;
    }

    public string SelectedCampusId => _selectedCampusId;

    public IReadOnlyList<CampusDto>? CachedCampuses => _campuses;

    public CampusDto? SelectedCampus =>
        _campuses?.FirstOrDefault(c => string.Equals(c.Id, _selectedCampusId, StringComparison.Ordinal));

    public event EventHandler? SelectedCampusChanged;

    private bool HasPendingUpload => _preferences.GetString(PendingUploadKey) == PendingValue;

    public async Task<IReadOnlyList<CampusDto>> GetCampusesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _campuses is not null)
        {
            return _campuses;
        }

        var campuses = await _campusGateway.ListCampusesAsync(ct);
        if (campuses.Count > 0)
        {
            _campuses = campuses;
        }

        return campuses;
    }

    public async Task SelectCampusAsync(string campusId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campusId);

        await _gate.WaitAsync(ct);
        try
        {
            if (string.Equals(campusId, _selectedCampusId, StringComparison.Ordinal))
            {
                return;
            }

            if (_campuses is not null && !_campuses.Any(c => string.Equals(c.Id, campusId, StringComparison.Ordinal)))
            {
                throw new ArgumentException($"Unknown campus '{campusId}'.", nameof(campusId));
            }

            var signedIn = await _sessionStore.HasValidTokenAsync(ct);
            if (signedIn)
            {
                // BR-CMP-02: the profile is the source of truth, so save it before changing anything locally.
                await UploadAsync(campusId, ct);
            }

            Store(campusId, pendingUpload: !signedIn);
        }
        finally
        {
            _gate.Release();
        }

        SelectedCampusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SynchronizeAsync(CancellationToken ct = default)
    {
        var changed = false;

        await _gate.WaitAsync(ct);
        try
        {
            if (!await _sessionStore.HasValidTokenAsync(ct))
            {
                return;
            }

            // Read inside the gate: a profile fetched earlier could already be older than the last selection.
            var profile = await _authGateway.GetCurrentUserProfileAsync(ct: ct);
            var remote = profile?.SelectedCampusId;
            if (string.IsNullOrWhiteSpace(remote))
            {
                return;
            }

            if (string.Equals(remote, _selectedCampusId, StringComparison.Ordinal))
            {
                _preferences.Remove(PendingUploadKey);
            }
            else if (HasPendingUpload && string.Equals(remote, DefaultCampusId, StringComparison.Ordinal))
            {
                // UC-02 A1: upload the choice made as a guest; on failure it stays pending for the next sync.
                await UploadAsync(_selectedCampusId, ct);
                _preferences.Remove(PendingUploadKey);
            }
            else
            {
                Store(remote, pendingUpload: false);
                changed = true;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger?.LogWarning(ex, "Could not synchronise the selected campus with the profile.");
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
        {
            SelectedCampusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Task UploadAsync(string campusId, CancellationToken ct) =>
        _profileGateway.UpdateMyProfileAsync(new UpdateMyProfileRequest(null, null, campusId, null), ct);

    private void Store(string campusId, bool pendingUpload)
    {
        _selectedCampusId = campusId;
        _preferences.SetString(SelectedCampusKey, campusId);

        if (pendingUpload)
        {
            _preferences.SetString(PendingUploadKey, PendingValue);
        }
        else
        {
            _preferences.Remove(PendingUploadKey);
        }
    }
}
