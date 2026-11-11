namespace RoomToRentRadar.Core.Models.Authentication;

public static class UserRoleConstants
{
    public const string Student = "STUDENT";
    public const string Landlord = "LANDLORD";

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

public sealed record AuthResult(
    bool IsSuccess,
    string? AccessToken = null,
    string? RefreshToken = null,
    AuthUser? User = null,
    string? ErrorMessage = null,
    bool RequiresRoleSelection = false,
    string? PendingGoogleIdToken = null)
{
    public static AuthResult Success(string accessToken, string refreshToken, AuthUser user) =>
        new(true, AccessToken: accessToken, RefreshToken: refreshToken, User: user);

    public static AuthResult Failed(string errorMessage) =>
        new(false, ErrorMessage: errorMessage);

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
