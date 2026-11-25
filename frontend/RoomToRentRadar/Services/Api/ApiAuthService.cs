using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace RoomToRentRadar.Services.Api
{
    /// <summary>
    /// Friendly error of the last failed auth call (US01 AC4). <see cref="IAuthService"/> only returns null
    /// on failure, so the Login/Register screens read the reason from here.
    /// </summary>
    public interface IAuthFeedback
    {
        string? LastErrorMessage { get; }
    }

    /// <summary>
    /// US01 / T01.2: <see cref="IAuthService"/> implemented over the backend Web API (AuthController, T01.1)
    /// instead of the local SQLite AuthService — the MAUI app is a pure frontend.
    /// Successful login/register stores the JWT + user via <see cref="AuthTokenStore"/> (SecureStorage, AC3);
    /// AuthTokenHandler then attaches the token to every later API request.
    /// </summary>
    public sealed class ApiAuthService : IAuthService, IAuthFeedback
    {
        private const string ConnectionError =
            "Không kết nối được máy chủ. Hãy kiểm tra mạng và chắc chắn backend đang chạy (docker compose up).";
        private const string UnexpectedError = "Đã có lỗi xảy ra. Vui lòng thử lại sau.";

        private readonly HttpClient _http;
        private readonly AuthTokenStore _tokens;
        private readonly ILogger<ApiAuthService> _logger;

        public ApiAuthService(HttpClient http, AuthTokenStore tokens, ILogger<ApiAuthService> logger)
        {
            _http = http;
            _tokens = tokens;
            _logger = logger;
        }

        /// <summary>Signed-in user from the saved session. Call <see cref="RestoreSessionAsync"/> at startup.</summary>
        public UserAccount? CurrentUser => _tokens.User;

        public bool IsSignedIn => CurrentUser is not null;

        public string? LastErrorMessage { get; private set; }

        /// <summary>Loads the session saved in SecureStorage (keeps the user signed in across app restarts).</summary>
        public Task RestoreSessionAsync() => _tokens.LoadAsync();

        public Task<UserAccount?> LoginAsync(string email, string password) =>
            SendAuthRequestAsync("api/auth/login", JsonContent.Create(
                new LoginBody(email?.Trim() ?? string.Empty, password ?? string.Empty),
                AuthJsonContext.Default.LoginBody));

        public Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role) =>
            SendAuthRequestAsync("api/auth/register", JsonContent.Create(
                new RegisterBody(
                    fullName?.Trim() ?? string.Empty,
                    email?.Trim() ?? string.Empty,
                    password ?? string.Empty,
                    phone?.Trim() ?? string.Empty,
                    role?.Trim() ?? string.Empty),
                AuthJsonContext.Default.RegisterBody));

        public void Logout()
        {
            // JWT is stateless: signing out means forgetting the token on this device.
            _tokens.Clear();
            LastErrorMessage = null;
            _logger.LogInformation("User signed out.");
        }

        private async Task<UserAccount?> SendAuthRequestAsync(string path, HttpContent body)
        {
            LastErrorMessage = null;

            try
            {
                using var response = await _http.PostAsync(path, body);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync(AuthJsonContext.Default.AuthTokenDto);
                    if (result?.User is null || string.IsNullOrEmpty(result.AccessToken))
                    {
                        LastErrorMessage = UnexpectedError;
                        _logger.LogWarning("Auth response from {Path} had no token or user.", path);
                        return null;
                    }

                    var user = ToUserAccount(result.User);
                    await _tokens.SaveAsync(result.AccessToken, result.ExpiresAtUtc, user);
                    _logger.LogInformation("User {UserId} signed in with role {Role}.", user.Id, user.Role);
                    return user;
                }

                // 400/401 carry a Vietnamese { message } from the API (validation, wrong password, email used).
                LastErrorMessage = await ReadErrorMessageAsync(response) ?? UnexpectedError;
                _logger.LogInformation("Auth request {Path} failed with {Status}.", path, (int)response.StatusCode);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // No network, backend not running, or timeout.
                LastErrorMessage = ConnectionError;
                _logger.LogWarning(ex, "Could not reach the API at {BaseAddress}.", _http.BaseAddress);
                return null;
            }
            catch (JsonException ex)
            {
                LastErrorMessage = UnexpectedError;
                _logger.LogError(ex, "Invalid JSON in auth response from {Path}.", path);
                return null;
            }
        }

        private async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
        {
            try
            {
                var error = await response.Content.ReadFromJsonAsync(AuthJsonContext.Default.ErrorDto);
                return string.IsNullOrWhiteSpace(error?.Message) ? null : error.Message;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Error response without a JSON message ({Status}).", (int)response.StatusCode);
                return null;
            }
        }

        private static UserAccount ToUserAccount(UserDto dto) => new()
        {
            Id = dto.Id,
            FullName = dto.FullName ?? string.Empty,
            Email = dto.Email ?? string.Empty,
            Phone = dto.Phone ?? string.Empty,
            Role = dto.Role ?? string.Empty,
            SelectedCampus = dto.SelectedCampus ?? "ICTU",
        };
    }

    // ---- JSON contract of AuthController (RoomToRentRadar.Api/Models/AuthModels.cs) ----

    internal sealed record LoginBody(string Email, string Password);

    internal sealed record RegisterBody(string FullName, string Email, string Password, string Phone, string Role);

    internal sealed record UserDto(int Id, string? FullName, string? Email, string? Phone, string? Role, string? SelectedCampus);

    internal sealed record AuthTokenDto(string AccessToken, string? TokenType, DateTime ExpiresAtUtc, UserDto? User);

    internal sealed record ErrorDto(string? Message);

    /// <summary>
    /// Source-generated JSON (camelCase like the API): no reflection, so it is safe with Android trimming.
    /// </summary>
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(LoginBody))]
    [JsonSerializable(typeof(RegisterBody))]
    [JsonSerializable(typeof(AuthTokenDto))]
    [JsonSerializable(typeof(ErrorDto))]
    internal sealed partial class AuthJsonContext : JsonSerializerContext
    {
    }
}
