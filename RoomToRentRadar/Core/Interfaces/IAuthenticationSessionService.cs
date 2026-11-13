using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// Manages the in-memory shared authentication state and persists sessions securely.
/// </summary>
public interface IAuthenticationSessionService
{
    /// <summary>
    /// Gets whether there is currently an authenticated user session.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current authenticated user's profile, or null if unauthenticated.
    /// </summary>
    AuthUser? CurrentUser { get; }

    /// <summary>
    /// Event raised when the current authenticated user or authentication status changes.
    /// </summary>
    event EventHandler<AuthUser?>? CurrentUserChanged;

    /// <summary>
    /// Securely persists access token, refresh token, and user state, updating the active session.
    /// </summary>
    Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active access token, if any.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active refresh token, if any.
    /// </summary>
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and restores the saved session upon app startup, verifying expiration or refreshing if needed.
    /// </summary>
    Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly triggers a token refresh using the stored refresh token.
    /// Updates tokens and session on success, or clears session on failure.
    /// </summary>
    Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the active session and removes stored credentials from secure storage.
    /// </summary>
    Task ClearSessionAsync(CancellationToken cancellationToken = default);
}
