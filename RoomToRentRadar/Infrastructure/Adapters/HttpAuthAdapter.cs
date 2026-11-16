using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Infrastructure.Adapters;

/// <summary>
/// Outbound HTTP Adapter implementing IAuthenticationPort to communicate with backend auth endpoints.
/// </summary>
public class HttpAuthAdapter : IAuthenticationPort
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpAuthAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                email = request.Email,
                password = request.Password,
                role = request.Role
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/register", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var userResponse = await response.Content.ReadFromJsonAsync<BackendRegisterResponseDto>(JsonOptions, cancellationToken);
                if (userResponse is null ||
                    userResponse.UserId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(userResponse.Email) ||
                    string.IsNullOrWhiteSpace(userResponse.Role))
                {
                    return AuthResult.Failed("Invalid registration response from authentication service.");
                }

                var user = new AuthUser(
                    userResponse.UserId,
                    userResponse.Email,
                    userResponse.Role,
                    "LOCAL",
                    true);

                return AuthResult.Success(string.Empty, string.Empty, user);
            }

            var errorDetails = await ParseErrorResponseAsync(response, cancellationToken);
            if ((int)response.StatusCode >= 500)
            {
                return AuthResult.ServerFailure(errorDetails.Message);
            }

            return AuthResult.Failed(errorDetails.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.TimeoutFailure("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException ex)
        {
            return AuthResult.NetworkFailure($"Unable to connect to authentication server. Please ensure the backend API is running. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message, AuthFailureType.Unknown);
        }
    }

    public async Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                email = request.Email,
                password = request.Password
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var authDto = await response.Content.ReadFromJsonAsync<BackendAuthSessionDto>(JsonOptions, cancellationToken);
                return BuildSessionAuthResult(authDto, "LOCAL");
            }

            var errorDetails = await ParseErrorResponseAsync(response, cancellationToken);
            if ((int)response.StatusCode >= 500)
            {
                return AuthResult.ServerFailure(errorDetails.Message);
            }

            return AuthResult.Failed(errorDetails.Message, AuthFailureType.InvalidCredentials);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.TimeoutFailure("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException ex)
        {
            return AuthResult.NetworkFailure($"Unable to connect to authentication server. Please ensure the backend API is running. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message, AuthFailureType.Unknown);
        }
    }

    public async Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                idToken = request.IdToken,
                role = request.Role
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/google", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var authDto = await response.Content.ReadFromJsonAsync<BackendAuthSessionDto>(JsonOptions, cancellationToken);
                return BuildSessionAuthResult(authDto, "GOOGLE");
            }

            var errorDetails = await ParseErrorResponseAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && errorDetails.HasRoleValidationError)
            {
                return AuthResult.NeedRoleSelection(request.IdToken);
            }

            if ((int)response.StatusCode >= 500)
            {
                return AuthResult.ServerFailure(errorDetails.Message);
            }

            return AuthResult.Failed(errorDetails.Message, AuthFailureType.InvalidCredentials);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.TimeoutFailure("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException ex)
        {
            return AuthResult.NetworkFailure($"Unable to connect to authentication server. Please ensure the backend API is running. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message, AuthFailureType.Unknown);
        }
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var authDto = await response.Content.ReadFromJsonAsync<BackendAuthSessionDto>(JsonOptions, cancellationToken);
                return BuildSessionAuthResult(authDto, "REFRESH");
            }

            var errorDetails = await ParseErrorResponseAsync(response, cancellationToken);

            if ((int)response.StatusCode >= 500)
            {
                return AuthResult.ServerFailure(errorDetails.Message);
            }

            if (errorDetails.IsTokenExpiredOrRevoked)
            {
                return AuthResult.InvalidToken(errorDetails.Message);
            }

            return response.StatusCode switch
            {
                HttpStatusCode.BadRequest => AuthResult.BadRequest(errorDetails.Message),
                HttpStatusCode.Forbidden => AuthResult.Forbidden(errorDetails.Message),
                HttpStatusCode.Unauthorized => AuthResult.Failed(errorDetails.Message, AuthFailureType.InvalidCredentials),
                _ => AuthResult.Failed(errorDetails.Message, AuthFailureType.Unknown)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.TimeoutFailure("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException ex)
        {
            return AuthResult.NetworkFailure($"Unable to connect to authentication server. Please ensure the backend API is running. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message, AuthFailureType.Unknown);
        }
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/logout", payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static AuthResult BuildSessionAuthResult(BackendAuthSessionDto? authDto, string provider)
    {
        if (authDto is null ||
            authDto.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(authDto.Email) ||
            string.IsNullOrWhiteSpace(authDto.Role) ||
            string.IsNullOrWhiteSpace(authDto.AccessToken))
        {
            return AuthResult.Failed("Invalid authentication response from authentication service.");
        }

        var user = new AuthUser(
            authDto.UserId,
            authDto.Email,
            authDto.Role,
            provider,
            true);

        return AuthResult.Success(authDto.AccessToken, authDto.RefreshToken, user);
    }

    private static readonly string[] TokenErrorKeywords =
    [
        "invalid_grant",
        "invalid_token",
        "invalid refresh token",
        "refresh token expired",
        "refresh token has expired",
        "refresh token revoked",
        "refresh token has been revoked",
        "the refresh token is invalid",
        "the refresh token has already been rotated or revoked",
        "token has expired",
        "token expired",
        "token has been revoked",
        "token revoked"
    ];

    private sealed record AuthErrorDetails(
        string Message,
        bool HasRoleValidationError,
        string? ErrorCode,
        bool IsTokenExpiredOrRevoked);

    private static async Task<AuthErrorDetails> ParseErrorResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fallbackMessage = $"Authentication request failed with status code {(int)response.StatusCode}.";

        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AuthErrorDetails(fallbackMessage, false, null, false);
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var hasRoleError = root.TryGetProperty("errors", out var errorsProp) &&
                               errorsProp.ValueKind == JsonValueKind.Object &&
                               errorsProp.EnumerateObject().Any(p =>
                                   string.Equals(p.Name, "role", StringComparison.OrdinalIgnoreCase));

            string? errorCode = null;
            if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
            {
                errorCode = errorProp.GetString();
            }
            else if (root.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
            {
                errorCode = codeProp.GetString();
            }
            else if (root.TryGetProperty("errorCode", out var errCodeProp) && errCodeProp.ValueKind == JsonValueKind.String)
            {
                errorCode = errCodeProp.GetString();
            }

            string? detail = null;
            if (root.TryGetProperty("detail", out var detailProp) && !string.IsNullOrWhiteSpace(detailProp.GetString()))
            {
                detail = detailProp.GetString();
            }

            string? title = null;
            if (root.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
            {
                title = titleProp.GetString();
            }

            string? message = null;
            if (root.TryGetProperty("message", out var messageProp) && !string.IsNullOrWhiteSpace(messageProp.GetString()))
            {
                message = messageProp.GetString();
            }

            string? errorDescription = null;
            if (root.TryGetProperty("error_description", out var descProp) && !string.IsNullOrWhiteSpace(descProp.GetString()))
            {
                errorDescription = descProp.GetString();
            }

            var chosenMessage = detail ?? title ?? message ?? errorDescription ?? content;

            var isTokenExpiredOrRevoked = DetermineIfTokenExpiredOrRevoked(
                errorCode,
                title,
                detail,
                message,
                errorDescription,
                content);

            return new AuthErrorDetails(chosenMessage, hasRoleError, errorCode, isTokenExpiredOrRevoked);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new AuthErrorDetails(fallbackMessage, false, null, false);
        }
    }

    private static bool DetermineIfTokenExpiredOrRevoked(params string?[] candidates)
    {
        foreach (var text in candidates)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (var keyword in TokenErrorKeywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class BackendAuthSessionDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class BackendRegisterResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
