using Microsoft.Maui.Authentication;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary>
/// Client adapter for Google OAuth 2.0 via .NET MAUI WebAuthenticator.
/// Interacts directly with Google's authorization endpoint to obtain a real OIDC ID token.
/// </summary>
public sealed class WebAuthenticatorGoogleAuthClientService : IGoogleAuthClientService
{
    private readonly string _clientId;
    private readonly string _redirectUri;

    public WebAuthenticatorGoogleAuthClientService(string? clientId = null, string? redirectUri = null)
    {
        _clientId = clientId
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? string.Empty;
        _redirectUri = redirectUri
            ?? Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI")
            ?? "com.companyname.roomtorentradar:/oauth2redirect";
    }

    public async Task<string?> AuthenticateAndGetIdTokenAsync(string? roleHint = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_clientId) ||
            _clientId.Contains("your-google-client-id", StringComparison.OrdinalIgnoreCase) ||
            _clientId.Contains("roomtorentradar-google-client-id", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(AppResources.GoogleClientIdMissing);
        }

        try
        {
            // Note: Do not pass 'hd=ictu.edu.vn' in the URL on Android because Chrome's internal
            // account consistency returns HTTP 204 (No Content) if the domain account is not yet
            // registered in Android system accounts. Domain enforcement is handled by backend (BR-AUTH-04).
            var authUrl = new Uri($"https://accounts.google.com/o/oauth2/v2/auth?" +
                                  $"client_id={Uri.EscapeDataString(_clientId)}" +
                                  $"&response_type={Uri.EscapeDataString("code id_token")}" +
                                  $"&scope=openid%20email%20profile" +
                                  $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                                  $"&nonce={Guid.NewGuid():N}" +
                                  $"&prompt=select_account");

            var callbackUrl = new Uri(_redirectUri);

            var authResult = await WebAuthenticator.Default.AuthenticateAsync(authUrl, callbackUrl);
            if (authResult?.Properties != null)
            {
                if (authResult.Properties.TryGetValue("error", out var errorMsg))
                {
                    var desc = authResult.Properties.TryGetValue("error_description", out var d) ? d : errorMsg;
                    throw new InvalidOperationException($"Google OAuth Error: {desc}");
                }

                if (authResult.Properties.TryGetValue("id_token", out var idToken) && !string.IsNullOrWhiteSpace(idToken))
                {
                    return idToken;
                }
            }

            return null;
        }
        catch (TaskCanceledException)
        {
            // User closed the browser or cancelled the login prompt
            return null;
        }
    }
}
