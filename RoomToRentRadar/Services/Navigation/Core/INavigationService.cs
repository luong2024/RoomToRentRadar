namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// Shell navigation abstraction so PageModels never call Shell.Current directly (ADR-003, T00.15).
/// Implemented by NavigationService in a later phase; kept pure C# here for unit testing.
/// </summary>
public interface INavigationService
{
    /// <summary>Pushes a detail route, optionally with query parameters (e.g. ?id=).</summary>
    Task GoToAsync(string route, IDictionary<string, object>? query = null);

    /// <summary>Pops the current page (Shell "..").</summary>
    Task GoBackAsync();

    /// <summary>Switches to a bottom tab, enforcing <see cref="NavigationPolicy.CanAccess"/>.</summary>
    Task GoToTabAsync(string route);

    /// <summary>
    /// BR-AUTH-07: returns true when already signed in; otherwise remembers <paramref name="returnRoute"/>,
    /// navigates to sign-in and returns false.
    /// </summary>
    Task<bool> RequireSignInAsync(string returnRoute);

    /// <summary>After a successful sign-in, returns to the remembered route (or explore).</summary>
    Task CompleteSignInAsync();

    /// <summary>Signs out and returns to the guest tab set at //explore.</summary>
    Task SignOutToGuestAsync();
}