namespace RoomToRentRadar.DeviceAdapters;

/// <summary>
/// Production Google authentication client service using WebAuthenticator.
/// Authenticates the user with real Google OAuth and retrieves a cryptographically verifiable ID token.
/// </summary>
public sealed class GoogleAuthClientService : IGoogleAuthClientService
{
    private readonly WebAuthenticatorGoogleAuthClientService _webAuthenticatorService;

    public GoogleAuthClientService(WebAuthenticatorGoogleAuthClientService? webAuthenticatorService = null)
    {
        _webAuthenticatorService = webAuthenticatorService ?? new WebAuthenticatorGoogleAuthClientService();
    }

    public async Task<string?> AuthenticateAndGetIdTokenAsync(string? roleHint = null, CancellationToken ct = default)
    {
        return await _webAuthenticatorService.AuthenticateAndGetIdTokenAsync(roleHint, ct);
    }
}
