using System.Globalization;
using Microsoft.Extensions.Logging;

namespace RoomToRentRadar.Services.Api
{
    /// <summary>
    /// US01 / T01.2 (AC3): keeps the JWT access token and the signed-in user in <see cref="SecureStorage"/>
    /// (Android Keystore / Windows DPAPI), with an in-memory copy so HTTP calls and
    /// <c>IAuthService.CurrentUser</c> can read it without awaiting.
    /// Call <see cref="LoadAsync"/> once before reading (ApiAuthService and AuthTokenHandler do this).
    /// An expired token is treated as "not signed in" and removed.
    /// </summary>
    public sealed class AuthTokenStore
    {
        private const string TokenKey = "auth_access_token";
        private const string ExpiresKey = "auth_expires_at_utc";
        private const string UserIdKey = "auth_user_id";
        private const string FullNameKey = "auth_user_full_name";
        private const string EmailKey = "auth_user_email";
        private const string PhoneKey = "auth_user_phone";
        private const string RoleKey = "auth_user_role";
        private const string CampusKey = "auth_user_campus";

        private static readonly string[] AllKeys =
            [TokenKey, ExpiresKey, UserIdKey, FullNameKey, EmailKey, PhoneKey, RoleKey, CampusKey];

        private readonly ILogger<AuthTokenStore> _logger;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private bool _loaded;

        public AuthTokenStore(ILogger<AuthTokenStore> logger)
        {
            _logger = logger;
        }

        /// <summary>Current token, or null when signed out or expired.</summary>
        public string? AccessToken => IsExpired ? null : _accessToken;

        /// <summary>User returned by the API at login/register (never contains a password hash).</summary>
        public UserAccount? User => IsExpired ? null : _user;

        public DateTime? ExpiresAtUtc { get; private set; }

        private string? _accessToken;
        private UserAccount? _user;

        // One minute of margin so a request does not leave with a token that expires on the way.
        private bool IsExpired => ExpiresAtUtc is null || ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(1);

        /// <summary>Reads the saved session from SecureStorage once. Safe to call many times.</summary>
        public async Task LoadAsync()
        {
            if (_loaded)
            {
                return;
            }

            await _loadLock.WaitAsync();
            try
            {
                if (_loaded)
                {
                    return;
                }

                var token = await SecureStorage.Default.GetAsync(TokenKey);
                var expiresText = await SecureStorage.Default.GetAsync(ExpiresKey);

                if (string.IsNullOrEmpty(token)
                    || !DateTime.TryParse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires))
                {
                    ClearMemory();
                }
                else
                {
                    _accessToken = token;
                    ExpiresAtUtc = expires.ToUniversalTime();
                    _user = new UserAccount
                    {
                        Id = int.TryParse(await SecureStorage.Default.GetAsync(UserIdKey), out var id) ? id : 0,
                        FullName = await SecureStorage.Default.GetAsync(FullNameKey) ?? string.Empty,
                        Email = await SecureStorage.Default.GetAsync(EmailKey) ?? string.Empty,
                        Phone = await SecureStorage.Default.GetAsync(PhoneKey) ?? string.Empty,
                        Role = await SecureStorage.Default.GetAsync(RoleKey) ?? string.Empty,
                        SelectedCampus = await SecureStorage.Default.GetAsync(CampusKey) ?? "ICTU",
                    };

                    if (IsExpired)
                    {
                        _logger.LogInformation("Saved session has expired; signing out.");
                        RemoveAllKeys();
                        ClearMemory();
                    }
                }
            }
            catch (Exception ex)
            {
                // SecureStorage can fail (e.g. keystore reset after an app reinstall): start signed out.
                _logger.LogWarning(ex, "Could not read the saved session from SecureStorage.");
                RemoveAllKeys();
                ClearMemory();
            }
            finally
            {
                _loaded = true;
                _loadLock.Release();
            }
        }

        /// <summary>Saves a new session after a successful login or registration.</summary>
        public async Task SaveAsync(string accessToken, DateTime expiresAtUtc, UserAccount user)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
            ArgumentNullException.ThrowIfNull(user);

            // Memory first: the session works for this run even if SecureStorage fails.
            _accessToken = accessToken;
            ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
            _user = user;
            _loaded = true;

            try
            {
                await SecureStorage.Default.SetAsync(TokenKey, accessToken);
                await SecureStorage.Default.SetAsync(ExpiresKey, ExpiresAtUtc.Value.ToString("O", CultureInfo.InvariantCulture));
                await SecureStorage.Default.SetAsync(UserIdKey, user.Id.ToString(CultureInfo.InvariantCulture));
                await SecureStorage.Default.SetAsync(FullNameKey, user.FullName);
                await SecureStorage.Default.SetAsync(EmailKey, user.Email);
                await SecureStorage.Default.SetAsync(PhoneKey, user.Phone);
                await SecureStorage.Default.SetAsync(RoleKey, user.Role);
                await SecureStorage.Default.SetAsync(CampusKey, user.SelectedCampus);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist the session; the user stays signed in until the app closes.");
            }
        }

        /// <summary>Signs out: removes the token and user from memory and SecureStorage.</summary>
        public void Clear()
        {
            ClearMemory();
            _loaded = true;
            RemoveAllKeys();
        }

        private void ClearMemory()
        {
            _accessToken = null;
            _user = null;
            ExpiresAtUtc = null;
        }

        private void RemoveAllKeys()
        {
            try
            {
                foreach (var key in AllKeys)
                {
                    SecureStorage.Default.Remove(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove the session from SecureStorage.");
            }
        }
    }
}
