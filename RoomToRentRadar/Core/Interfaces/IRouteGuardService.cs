using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// Status returned when evaluating a route's access policy against an optional authenticated user.
/// </summary>
public enum RouteAccessStatus
{
    /// <summary>
    /// The route is accessible to anyone or the user meets the authentication/role requirements.
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// The route is protected and requires an authenticated user session.
    /// </summary>
    RequiresAuthentication,

    /// <summary>
    /// The route requires specific roles that the current user does not possess.
    /// </summary>
    AccessDeniedRoleMismatch,

    /// <summary>
    /// The route is for unauthenticated guests only (e.g. login/register) and authenticated users should be redirected to Home.
    /// </summary>
    RedirectGuestOnlyToHome
}

/// <summary>
/// Represents the navigation decision produced by the route guard after session hydration.
/// </summary>
public sealed record RouteGuardDecision(
    RouteAccessStatus Status,
    bool IsAllowed,
    string? RedirectRoute = null,
    string? AlertMessage = null);

/// <summary>
/// Centrally evaluates route access rules, roles, and guest/authenticated boundaries.
/// </summary>
public interface IRouteGuardService
{
    /// <summary>
    /// Evaluates whether the specified route target can be accessed by the given user.
    /// </summary>
    RouteAccessStatus EvaluateRouteAccess(string? routeUri, AuthUser? currentUser);

    /// <summary>
    /// Awaits session hydration and evaluates whether navigation to the target route is permitted,
    /// returning the redirect target (preserving returnUrl when unauthenticated) and optional alert message.
    /// </summary>
    Task<RouteGuardDecision> InterceptNavigationAsync(string? routeUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the destination route after login, validating any preserved returnUrl against role and guest-only rules.
    /// </summary>
    string ResolvePostLoginRedirectRoute(string? returnUrl, AuthUser? authenticatedUser);
}
