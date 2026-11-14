using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Infrastructure.Adapters;

/// <summary>
/// Infrastructure adapter handling Google OAuth 2.0 + PKCE authentication on mobile platforms via MAUI WebAuthenticator.
/// </summary>
public class GoogleAuthAdapter : IGoogleAuthAdapter
{
    private const string GoogleAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DefaultClientId = "69901634703-vt9nmqanpda8i8ai0qpkc3viqnfje6bb.apps.googleusercontent.com";

    private readonly string _clientId;
    private readonly HttpClient _oauthHttpClient;

    public GoogleAuthAdapter()
        : this(DefaultClientId, null)
    {
    }

    public GoogleAuthAdapter(string? clientId, HttpClient? oauthHttpClient = null)
    {
        _clientId = !string.IsNullOrWhiteSpace(clientId) ? clientId.Trim() : DefaultClientId;
        _oauthHttpClient = oauthHttpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public virtual async Task<GoogleAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_clientId))
            {
                return GoogleAuthResult.Failed("Google OAuth Client ID is not configured.");
            }

            var redirectUri = BuildRedirectUri(_clientId);
            var codeVerifier = GenerateBase64UrlRandom(32);
            var codeChallenge = ComputeCodeChallenge(codeVerifier);
            var state = GenerateBase64UrlRandom(16);

            var authorizeUrl = BuildAuthorizeUrl(_clientId, redirectUri, codeChallenge, state);

            var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(authorizeUrl),
                new Uri(redirectUri));

            if (authResult.Properties.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
            {
                return GoogleAuthResult.Failed($"Google sign-in failed: {error}");
            }

            if (!authResult.Properties.TryGetValue("state", out var returnedState) ||
                !string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                return GoogleAuthResult.Failed("Google sign-in failed due to an invalid OAuth state.");
            }

            if (!authResult.Properties.TryGetValue("code", out var authorizationCode) ||
                string.IsNullOrWhiteSpace(authorizationCode))
            {
                return GoogleAuthResult.Failed("Google sign-in did not return an authorization code.");
            }

            return await ExchangeCodeForIdTokenAsync(authorizationCode, codeVerifier, redirectUri, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return GoogleAuthResult.Canceled();
        }
        catch (OperationCanceledException)
        {
            return GoogleAuthResult.Canceled();
        }
        catch (NotImplementedException)
        {
            return GoogleAuthResult.Failed("Google sign-in via WebAuthenticator is not supported on this desktop platform.");
        }
        catch (PlatformNotSupportedException)
        {
            return GoogleAuthResult.Failed("Google sign-in via WebAuthenticator is not supported on this platform.");
        }
        catch (Exception ex)
        {
            return GoogleAuthResult.Failed($"Google sign-in error: {ex.Message}");
        }
    }

    private async Task<GoogleAuthResult> ExchangeCodeForIdTokenAsync(
        string authorizationCode,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        using var tokenRequestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["code"] = authorizationCode,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _oauthHttpClient.PostAsync(GoogleTokenEndpoint, tokenRequestContent, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return GoogleAuthResult.Failed($"Google token exchange failed ({(int)response.StatusCode}).");
        }

        using var json = JsonDocument.Parse(responseBody);
        if (!json.RootElement.TryGetProperty("id_token", out var idTokenElement) ||
            string.IsNullOrWhiteSpace(idTokenElement.GetString()))
        {
            return GoogleAuthResult.Failed("Google token response did not include an id_token.");
        }

        return GoogleAuthResult.Success(idTokenElement.GetString()!);
    }

    private static string BuildRedirectUri(string clientId)
    {
        const string googleDomainSuffix = ".apps.googleusercontent.com";
        if (clientId.EndsWith(googleDomainSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var clientPrefix = clientId[..^googleDomainSuffix.Length];
            return $"com.googleusercontent.apps.{clientPrefix}:/oauth2redirect";
        }

        return "com.companyname.roomtorentradar:/oauth2redirect";
    }

    private static string BuildAuthorizeUrl(string clientId, string redirectUri, string codeChallenge, string state)
    {
        var query = string.Join("&",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            $"scope={Uri.EscapeDataString("openid email profile")}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
            $"state={Uri.EscapeDataString(state)}");

        return $"{GoogleAuthorizationEndpoint}?{query}";
    }

    private static string GenerateBase64UrlRandom(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    private static string ComputeCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
