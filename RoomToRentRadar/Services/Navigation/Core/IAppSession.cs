namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// Current session role for role-aware navigation (T00.15). Pure C#; UI subscribers marshal
/// <see cref="RoleChanged"/> / <see cref="SessionEnded"/> to the UI thread themselves.
/// </summary>
public interface IAppSession
{
    /// <summary>The signed-in role ("Student" | "Landlord" | "Admin"), or null for a guest.</summary>
    string? CurrentRole { get; }

    /// <summary>Raised when <see cref="CurrentRole"/> changes (sign-in, sign-out, expiry).</summary>
    event EventHandler? RoleChanged;

    /// <summary>Raised when the session expires definitively (ISessionEvents.SessionExpired).</summary>
    event EventHandler? SessionEnded;

    /// <summary>Reloads the role from the cached profile; falls back to guest on error/no profile.</summary>
    Task RefreshAsync(CancellationToken ct = default);
}