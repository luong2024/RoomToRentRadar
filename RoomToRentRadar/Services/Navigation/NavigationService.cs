using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.Services.Navigation;

/// <summary>
/// Shell-backed <see cref="INavigationService"/> (T00.15). Role checks here are UI guards only;
/// the server remains the security authority for role-gated data.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IAppSession _appSession;
    private readonly ISessionStore _sessionStore;

    public NavigationService(IAppSession appSession, ISessionStore sessionStore)
    {
        _appSession = appSession ?? throw new ArgumentNullException(nameof(appSession));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    }

    /// <summary>The route a guest wanted before being sent to sign-in (BR-AUTH-07).</summary>
    public string? PendingReturnRoute { get; private set; }

    public Task GoToAsync(string route, IDictionary<string, object>? query = null) =>
        query is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, query);

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");

    public async Task GoToTabAsync(string route)
    {
        if (!NavigationPolicy.CanAccess(_appSession.CurrentRole, route))
        {
            return;
        }

        await Shell.Current.GoToAsync("//" + route);
    }

    public async Task<bool> RequireSignInAsync(string returnRoute)
    {
        if (await _sessionStore.HasValidTokenAsync())
        {
            return true;
        }

        PendingReturnRoute = returnRoute;
        await Shell.Current.GoToAsync("//" + AppRoutes.Login);
        return false;
    }

    public async Task CompleteSignInAsync()
    {
        await _appSession.RefreshAsync();
        var returnRoute = PendingReturnRoute;
        PendingReturnRoute = null;
        await Shell.Current.GoToAsync(returnRoute ?? "//" + AppRoutes.Explore);
    }

    public async Task SignOutToGuestAsync()
    {
        // Callers purge credentials first; refreshing the cleared store resets the role to guest.
        await _appSession.RefreshAsync();
        await Shell.Current.GoToAsync("//" + AppRoutes.Explore);
    }
}