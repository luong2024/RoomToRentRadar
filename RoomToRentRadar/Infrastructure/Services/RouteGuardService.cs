using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Infrastructure.Services;

/// <summary>
/// Evaluates route authorization rules, role restrictions, session hydration readiness,
/// and returnUrl preservation.
/// </summary>
public class RouteGuardService : IRouteGuardService
{
    public const string RoleMismatchAlertMessage =
        "This page is not available for your current account role.";

    private readonly IAuthenticationSessionService? _sessionService;

    public RouteGuardService()
    {
    }

    public RouteGuardService(IAuthenticationSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public RouteAccessStatus EvaluateRouteAccess(string? routeUri, AuthUser? currentUser)
    {
        if (string.IsNullOrWhiteSpace(routeUri))
        {
            return RouteAccessStatus.Allowed;
        }

        var normalizedRoute = NormalizeRoute(routeUri);
        if (string.IsNullOrEmpty(normalizedRoute))
        {
            return RouteAccessStatus.Allowed;
        }

        // 1. Guest-only routes (Login / Registration)
        if (IsGuestOnlyRoute(normalizedRoute))
        {
            return currentUser != null
                ? RouteAccessStatus.RedirectGuestOnlyToHome
                : RouteAccessStatus.Allowed;
        }

        // 2. Publicly accessible routes
        if (IsPublicRoute(normalizedRoute))
        {
            return RouteAccessStatus.Allowed;
        }

        // 3. Protected routes requiring authentication
        if (currentUser == null)
        {
            return RouteAccessStatus.RequiresAuthentication;
        }

        // 4. Role-restricted routes
        if (IsRoommateMatchingRoute(normalizedRoute))
        {
            var isStudentOrAdmin = string.Equals(currentUser.Role, UserRoleConstants.Student, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(currentUser.Role, "ADMIN", StringComparison.OrdinalIgnoreCase);

            return isStudentOrAdmin
                ? RouteAccessStatus.Allowed
                : RouteAccessStatus.AccessDeniedRoleMismatch;
        }

        // 5. Booking and Profile routes allow all authenticated roles (STUDENT, LANDLORD, ADMIN)
        if (IsBookingRoute(normalizedRoute) || IsProfileRoute(normalizedRoute))
        {
            return RouteAccessStatus.Allowed;
        }

        // Default: Unrecognized/open routes are permitted
        return RouteAccessStatus.Allowed;
    }

    public async Task<RouteGuardDecision> InterceptNavigationAsync(
        string? routeUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_sessionService != null)
            {
                await _sessionService.EnsureInitializedAsync(cancellationToken);
            }

            var currentUser = _sessionService?.CurrentUser;
            var status = EvaluateRouteAccess(routeUri, currentUser);

            return status switch
            {
                RouteAccessStatus.Allowed => new RouteGuardDecision(
                    RouteAccessStatus.Allowed,
                    IsAllowed: true),

                RouteAccessStatus.RequiresAuthentication => new RouteGuardDecision(
                    RouteAccessStatus.RequiresAuthentication,
                    IsAllowed: false,
                    RedirectRoute: BuildLoginRedirectRoute(routeUri)),

                RouteAccessStatus.RedirectGuestOnlyToHome => new RouteGuardDecision(
                    RouteAccessStatus.RedirectGuestOnlyToHome,
                    IsAllowed: false,
                    RedirectRoute: $"//{AppRoutes.Home}"),

                RouteAccessStatus.AccessDeniedRoleMismatch => new RouteGuardDecision(
                    RouteAccessStatus.AccessDeniedRoleMismatch,
                    IsAllowed: false,
                    RedirectRoute: $"//{AppRoutes.Home}",
                    AlertMessage: RoleMismatchAlertMessage),

                _ => new RouteGuardDecision(
                    RouteAccessStatus.RequiresAuthentication,
                    IsAllowed: false,
                    RedirectRoute: $"//{AppRoutes.Home}")
            };
        }
        catch
        {
            // Fail-closed: cancel navigation and redirect to a safe fallback route when guard check fails
            return new RouteGuardDecision(
                RouteAccessStatus.RequiresAuthentication,
                IsAllowed: false,
                RedirectRoute: $"//{AppRoutes.Home}");
        }
    }

    public string ResolvePostLoginRedirectRoute(string? returnUrl, AuthUser? authenticatedUser)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || authenticatedUser == null)
        {
            return $"//{AppRoutes.Home}";
        }

        var candidate = returnUrl.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || IsExternalOrInvalidUri(candidate))
        {
            return $"//{AppRoutes.Home}";
        }

        var normalizedBase = NormalizeRoute(candidate);
        if (string.IsNullOrEmpty(normalizedBase) || IsGuestOnlyRoute(normalizedBase))
        {
            return $"//{AppRoutes.Home}";
        }

        var accessStatus = EvaluateRouteAccess(candidate, authenticatedUser);
        if (accessStatus != RouteAccessStatus.Allowed)
        {
            return $"//{AppRoutes.Home}";
        }

        return FormatInternalNavigationRoute(candidate, normalizedBase);
    }

    public static string BuildLoginRedirectRoute(string? targetRouteUri)
    {
        var sanitized = SanitizeReturnUrl(targetRouteUri);
        if (string.IsNullOrEmpty(sanitized))
        {
            return $"//{AppRoutes.Login}";
        }

        return $"//{AppRoutes.Login}?returnUrl={Uri.EscapeDataString(sanitized)}";
    }

    public static string? SanitizeReturnUrl(string? rawRouteUri)
    {
        if (string.IsNullOrWhiteSpace(rawRouteUri))
        {
            return null;
        }

        var trimmed = rawRouteUri.Trim();
        if (IsExternalOrInvalidUri(trimmed))
        {
            return null;
        }

        var normalizedBase = NormalizeRoute(trimmed);
        if (string.IsNullOrEmpty(normalizedBase) || IsGuestOnlyRoute(normalizedBase))
        {
            return null;
        }

        return FormatInternalNavigationRoute(trimmed, normalizedBase);
    }

    public static string NormalizeRoute(string rawRoute)
    {
        var cleaned = rawRoute.Trim();

        // Strip query strings
        var queryIndex = cleaned.IndexOf('?');
        if (queryIndex >= 0)
        {
            cleaned = cleaned[..queryIndex];
        }

        // Strip fragments
        var hashIndex = cleaned.IndexOf('#');
        if (hashIndex >= 0)
        {
            cleaned = cleaned[..hashIndex];
        }

        // Strip leading "//" or "/"
        cleaned = cleaned.TrimStart('/');

        // Strip trailing "/"
        cleaned = cleaned.TrimEnd('/');

        return cleaned.ToLowerInvariant();
    }

    private static string FormatInternalNavigationRoute(string rawRouteWithQuery, string normalizedBase)
    {
        var querySuffix = string.Empty;
        var queryIndex = rawRouteWithQuery.IndexOf('?');
        if (queryIndex >= 0)
        {
            var fragmentIndex = rawRouteWithQuery.IndexOf('#', queryIndex);
            querySuffix = fragmentIndex >= 0
                ? rawRouteWithQuery[queryIndex..fragmentIndex]
                : rawRouteWithQuery[queryIndex..];
        }

        // Relative detail routes (room-detail and rooms/details) are pushed onto the stack without leading "//"
        if (normalizedBase == AppRoutes.RoomDetail.ToLowerInvariant() ||
            normalizedBase == AppRoutes.RoomDetails.ToLowerInvariant() ||
            normalizedBase == AppRoutes.CreateListing.ToLowerInvariant())
        {
            return $"{normalizedBase}{querySuffix}";
        }

        return $"//{normalizedBase}{querySuffix}";
    }

    private static bool IsExternalOrInvalidUri(string candidate)
    {
        var queryIndex = candidate.IndexOf('?');
        var pathSegment = queryIndex >= 0 ? candidate[..queryIndex] : candidate;

        return candidate.Contains("://", StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
               pathSegment.Contains(':', StringComparison.Ordinal) ||
               pathSegment.Contains('%', StringComparison.Ordinal);
    }

    private static bool IsGuestOnlyRoute(string normalizedRoute)
    {
        return normalizedRoute == AppRoutes.Login.ToLowerInvariant()
               || normalizedRoute == "login"
               || normalizedRoute == "auth"
               || normalizedRoute == "auth/login"
               || normalizedRoute == "register"
               || normalizedRoute == "auth/register";
    }

    private static bool IsPublicRoute(string normalizedRoute)
    {
        return normalizedRoute == AppRoutes.Home.ToLowerInvariant()
               || normalizedRoute == AppRoutes.RoomListings.ToLowerInvariant()
               || normalizedRoute == AppRoutes.RoomDetail.ToLowerInvariant()
               || normalizedRoute == AppRoutes.RoomDetails.ToLowerInvariant()
               || normalizedRoute == AppRoutes.CreateListing.ToLowerInvariant()
               || normalizedRoute == "create-listing";
    }

    private static bool IsProfileRoute(string normalizedRoute)
    {
        return normalizedRoute == AppRoutes.Profile.ToLowerInvariant()
               || normalizedRoute.StartsWith("profile/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBookingRoute(string normalizedRoute)
    {
        return normalizedRoute == AppRoutes.Booking.ToLowerInvariant()
               || normalizedRoute.StartsWith("booking/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoommateMatchingRoute(string normalizedRoute)
    {
        return normalizedRoute == AppRoutes.RoommateMatching.ToLowerInvariant()
               || normalizedRoute.StartsWith("roommates/", StringComparison.OrdinalIgnoreCase);
    }
}
