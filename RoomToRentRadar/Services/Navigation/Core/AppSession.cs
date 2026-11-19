using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// Default <see cref="IAppSession"/>. Reads the cached profile role and listens for definitive
/// session expiry. Stays pure C# (no MAUI): subscribers marshal events to the UI thread (T00.15).
/// </summary>
public sealed class AppSession : IAppSession
{
    private readonly ISessionStore _sessionStore;
    private string? _currentRole;

    public AppSession(ISessionStore sessionStore, ISessionEvents sessionEvents)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        ArgumentNullException.ThrowIfNull(sessionEvents);

        sessionEvents.SessionExpired += OnSessionExpired;
    }

    public string? CurrentRole => _currentRole;

    public event EventHandler? RoleChanged;

    public event EventHandler? SessionEnded;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        string? role;
        try
        {
            var profile = await _sessionStore.GetCachedUserProfileAsync(ct);
            role = profile?.Role;
        }
        catch
        {
            // Offline / store failure: fall back to guest rather than crash navigation.
            role = null;
        }

        SetRole(role);
    }

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        SetRole(null);
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    private void SetRole(string? role)
    {
        var normalized = string.IsNullOrWhiteSpace(role) ? null : role;
        if (string.Equals(_currentRole, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _currentRole = normalized;
        RoleChanged?.Invoke(this, EventArgs.Empty);
    }
}