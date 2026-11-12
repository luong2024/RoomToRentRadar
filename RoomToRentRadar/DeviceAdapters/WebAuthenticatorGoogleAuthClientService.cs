using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;

namespace RoomToRentRadar.DeviceAdapters;

public sealed class WebAuthenticatorGoogleAuthClientService : IGoogleAuthClientService
{
    private readonly string _clientId;
    private readonly string _redirectUri;

    public WebAuthenticatorGoogleAuthClientService(string? clientId = null, string? redirectUri = null)
    {
        _clientId = clientId ?? Preferences.Default.Get("GoogleClientId", "roomtorentradar-google-client-id");
        _redirectUri = redirectUri ?? Preferences.Default.Get("GoogleRedirectUri", "com.companyname.roomtorentradar:/oauth2redirect");
    }

    public async Task<string?> AuthenticateAndGetIdTokenAsync(string? roleHint = null, CancellationToken ct = default)
    {
        try
        {
            var hdParam = string.Equals(roleHint, "Student", StringComparison.OrdinalIgnoreCase)
                ? "&hd=ictu.edu.vn"
                : string.Empty;

            var authUrl = new Uri($"https://accounts.google.com/o/oauth2/v2/auth?" +
                                  $"client_id={Uri.EscapeDataString(_clientId)}" +
                                  $"&response_type=id_token" +
                                  $"&scope=openid%20email%20profile" +
                                  $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                                  $"&nonce={Guid.NewGuid():N}" +
                                  hdParam);

            var callbackUrl = new Uri(_redirectUri);

            var authResult = await WebAuthenticator.Default.AuthenticateAsync(authUrl, callbackUrl);
            if (authResult?.Properties != null && authResult.Properties.TryGetValue("id_token", out var idToken))
            {
                return idToken;
            }

            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}

