namespace RoomToRentRadar.Core.Models.Authentication;

public static class UserRoleConstants
{
    public const string Student = "STUDENT";
    public const string Landlord = "LANDLORD";
    public const string Admin = "ADMIN";

    public static bool IsValidRegistrationRole(string? role) =>
        role is Student or Landlord;
}

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string Role);

public sealed record LoginWithEmailRequest(
    string Email,
    string Password);

public sealed record LoginWithGoogleRequest(
    string IdToken,
    string? Role = null);

public sealed record AuthUser(
    Guid Id,
    string Email,
    string Role,
    string Provider,
    bool IsActive);

/// <summary>
/// Categorizes the exact nature of an authentication failure to distinguish between
/// permanent credential rejection and temporary network/infrastructure faults.
/// </summary>
public enum AuthFailureType
{
    None = 0,
    InvalidCredentials,
    RefreshTokenExpiredOrRevoked,
    NetworkError,
    Timeout,
    ServerError,
    BadRequest,
    Forbidden,
    Unknown
}

public sealed record AuthResult(
    bool IsSuccess,
    string? AccessToken = null,
    string? RefreshToken = null,
    AuthUser? User = null,
    string? ErrorMessage = null,
    bool RequiresRoleSelection = false,
    string? PendingGoogleIdToken = null,
    AuthFailureType FailureType = AuthFailureType.None)
{
    /// <summary>
    /// Indicates whether the failure is transient (e.g. network disconnection, timeout, server 5xx error)
    /// where credentials remain potentially valid and session data should NOT be purged.
    /// </summary>
    public bool IsTransientError =>
        FailureType is AuthFailureType.NetworkError or AuthFailureType.Timeout or AuthFailureType.ServerError;

    public static AuthResult Success(string accessToken, string refreshToken, AuthUser user) =>
        new(true, AccessToken: accessToken, RefreshToken: refreshToken, User: user);

    public static AuthResult Failed(string errorMessage, AuthFailureType failureType = AuthFailureType.InvalidCredentials) =>
        new(false, ErrorMessage: errorMessage, FailureType: failureType);

    public static AuthResult NetworkFailure(string errorMessage) =>
        new(false, ErrorMessage: errorMessage, FailureType: AuthFailureType.NetworkError);

    public static AuthResult TimeoutFailure(string errorMessage) =>
        new(false, ErrorMessage: errorMessage, FailureType: AuthFailureType.Timeout);

    public static AuthResult ServerFailure(string errorMessage) =>
        new(false, ErrorMessage: errorMessage, FailureType: AuthFailureType.ServerError);

    public static AuthResult InvalidToken(string errorMessage) =>
        new(false, ErrorMessage: errorMessage, FailureType: AuthFailureType.RefreshTokenExpiredOrRevoked);

    public static AuthResult BadRequest(string errorMessage) =>
        new(false, ErrorMessage: errorMessage, FailureType: AuthFailureType.BadRequest);

    public static AuthResult Forbidden(string errorMessage) =>
        new(false, ErrorMessage: errorMessage, FailureType: AuthFailureType.Forbidden);

    public static AuthResult NeedRoleSelection(string googleIdToken) =>
        new(false, RequiresRoleSelection: true, PendingGoogleIdToken: googleIdToken);
}

public sealed record GoogleAuthResult(
    bool IsSuccess,
    bool IsCanceled = false,
    string? IdToken = null,
    string? ErrorMessage = null)
{
    public static GoogleAuthResult Success(string idToken) =>
        new(true, IdToken: idToken);

    public static GoogleAuthResult Canceled() =>
        new(false, IsCanceled: true, ErrorMessage: "Google sign-in was canceled or failed. Please try again.");

    public static GoogleAuthResult Failed(string? errorMessage = null) =>
        new(false, ErrorMessage: errorMessage ?? "Google sign-in was canceled or failed. Please try again.");
}

/// <summary>
/// Result of a logout attempt distinguishing local storage cleanup from server-side token revocation.
/// </summary>
public sealed record LogoutResult(
    bool LocalSessionCleared,
    bool ServerRevoked,
    string? ErrorMessage = null);
