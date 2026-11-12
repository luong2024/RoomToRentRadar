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

            var (errorMessage, _) = await ParseErrorResponseAsync(response, cancellationToken);
            return AuthResult.Failed(errorMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.Failed("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException)
        {
            return AuthResult.Failed("Unable to connect to authentication server. Please ensure the backend API is running.");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message);
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

            var (errorMessage, _) = await ParseErrorResponseAsync(response, cancellationToken);
            return AuthResult.Failed(errorMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.Failed("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException)
        {
            return AuthResult.Failed("Unable to connect to authentication server. Please ensure the backend API is running.");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message);
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

            var (errorDetail, hasRoleValidationError) = await ParseErrorResponseAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && hasRoleValidationError)
            {
                return AuthResult.NeedRoleSelection(request.IdToken);
            }

            return AuthResult.Failed(errorDetail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthResult.Failed("Connection timed out. Please check that the backend server is running and reachable.");
        }
        catch (HttpRequestException)
        {
            return AuthResult.Failed("Unable to connect to authentication server. Please ensure the backend API is running.");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed(ex.Message);
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

    private static async Task<(string Message, bool HasRoleValidationError)> ParseErrorResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fallbackMessage = $"Authentication request failed with status code {(int)response.StatusCode}.";

        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return (fallbackMessage, false);
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var hasRoleError = root.TryGetProperty("errors", out var errorsProp) &&
                               errorsProp.ValueKind == JsonValueKind.Object &&
                               errorsProp.EnumerateObject().Any(p =>
                                   string.Equals(p.Name, "role", StringComparison.OrdinalIgnoreCase));

            if (root.TryGetProperty("detail", out var detailProp) && !string.IsNullOrWhiteSpace(detailProp.GetString()))
            {
                return (detailProp.GetString()!, hasRoleError);
            }

            if (root.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
            {
                return (titleProp.GetString()!, hasRoleError);
            }

            if (root.TryGetProperty("message", out var messageProp) && !string.IsNullOrWhiteSpace(messageProp.GetString()))
            {
                return (messageProp.GetString()!, hasRoleError);
            }

            return (content, hasRoleError);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return (fallbackMessage, false);
        }
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
