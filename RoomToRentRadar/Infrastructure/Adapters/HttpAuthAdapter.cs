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
                var userResponse = await response.Content.ReadFromJsonAsync<BackendUserDto>(JsonOptions, cancellationToken);
                var user = new AuthUser(
                    userResponse?.Id ?? Guid.NewGuid(),
                    userResponse?.Email ?? request.Email,
                    userResponse?.Role ?? request.Role,
                    userResponse?.Provider ?? "LOCAL",
                    userResponse?.IsActive ?? true);

                return AuthResult.Success(string.Empty, string.Empty, user);
            }

            var errorMessage = await ExtractErrorMessageAsync(response, cancellationToken);
            return AuthResult.Failed(errorMessage);
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
                var authDto = await response.Content.ReadFromJsonAsync<BackendAuthResponseDto>(JsonOptions, cancellationToken);
                if (authDto == null)
                {
                    return AuthResult.Failed("Empty response from authentication service.");
                }

                var user = new AuthUser(
                    authDto.User?.Id ?? Guid.NewGuid(),
                    authDto.User?.Email ?? request.Email,
                    authDto.User?.Role ?? string.Empty,
                    authDto.User?.Provider ?? "LOCAL",
                    authDto.User?.IsActive ?? true);

                return AuthResult.Success(authDto.AccessToken, authDto.RefreshToken, user);
            }

            var errorMessage = await ExtractErrorMessageAsync(response, cancellationToken);
            return AuthResult.Failed(errorMessage);
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
                role = request.Role ?? "STUDENT"
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/google", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var authDto = await response.Content.ReadFromJsonAsync<BackendAuthResponseDto>(JsonOptions, cancellationToken);
                if (authDto == null)
                {
                    return AuthResult.Failed("Empty response from authentication service.");
                }

                var user = new AuthUser(
                    authDto.User?.Id ?? Guid.NewGuid(),
                    authDto.User?.Email ?? string.Empty,
                    authDto.User?.Role ?? (request.Role ?? "STUDENT"),
                    authDto.User?.Provider ?? "GOOGLE",
                    authDto.User?.IsActive ?? true);

                return AuthResult.Success(authDto.AccessToken, authDto.RefreshToken, user);
            }

            var errorDetail = await ExtractErrorMessageAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest &&
                errorDetail.Contains("Role must be either STUDENT or LANDLORD", StringComparison.OrdinalIgnoreCase))
            {
                return AuthResult.NeedRoleSelection(request.IdToken);
            }

            return AuthResult.Failed(errorDetail);
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

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return $"Authentication request failed with status code {(int)response.StatusCode}.";
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("detail", out var detailProp) && !string.IsNullOrWhiteSpace(detailProp.GetString()))
            {
                return detailProp.GetString()!;
            }
            if (doc.RootElement.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
            {
                return titleProp.GetString()!;
            }
            if (doc.RootElement.TryGetProperty("message", out var messageProp) && !string.IsNullOrWhiteSpace(messageProp.GetString()))
            {
                return messageProp.GetString()!;
            }

            return content;
        }
        catch
        {
            return $"Authentication request failed with status code {(int)response.StatusCode}.";
        }
    }

    private sealed class BackendAuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public BackendUserDto? User { get; set; }
    }

    private sealed class BackendUserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
