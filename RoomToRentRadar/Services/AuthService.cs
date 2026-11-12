using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Data;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Local SQLite authentication and in-memory session for US01 / T01.1.
    /// Implements IAuthService from IRentalContracts.cs (consumed by T01.2, T03.2, T09.1).
    /// Per the contract, failures return null; the reason is written to the log.
    /// </summary>
    public class AuthService : IAuthService
    {
        public const int MinPasswordLength = 6;

        // Stored hash format: "PBKDF2$<iterations>$<saltBase64>$<hashBase64>"
        private const string HashPrefix = "PBKDF2";
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        // SQLite result code for a constraint violation (e.g. UNIQUE on Email).
        private const int SqliteConstraintError = 19;

        private readonly UserRepository _users;
        private readonly ILogger<AuthService> _logger;

        public AuthService(UserRepository users, ILogger<AuthService> logger)
        {
            _users = users;
            _logger = logger;
        }

        public UserAccount? CurrentUser { get; private set; }

        public bool IsSignedIn => CurrentUser is not null;

        public async Task<UserAccount?> LoginAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                _logger.LogInformation("Login rejected: email or password is empty.");
                return null;
            }

            var user = await _users.GetByEmailAsync(NormalizeEmail(email));
            if (user is null || !VerifyPassword(password, user.PasswordHash))
            {
                // Same log/result for "no such user" and "wrong password" so callers cannot probe emails.
                _logger.LogInformation("Login failed for a submitted email.");
                return null;
            }

            CurrentUser = WithoutPasswordHash(user);
            _logger.LogInformation("User {UserId} signed in with role {Role}.", user.Id, user.Role);
            return CurrentUser;
        }

        public async Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role)
        {
            var normalizedEmail = NormalizeEmail(email ?? string.Empty);
            var normalizedRole = NormalizeSelfRegisterRole(role);

            if (string.IsNullOrWhiteSpace(fullName)
                || !IsValidEmail(normalizedEmail)
                || string.IsNullOrEmpty(password) || password.Length < MinPasswordLength
                || string.IsNullOrWhiteSpace(phone)
                || normalizedRole is null)
            {
                _logger.LogInformation("Register rejected: invalid input or role not allowed.");
                return null;
            }

            if (await _users.EmailExistsAsync(normalizedEmail))
            {
                _logger.LogInformation("Register rejected: email already in use.");
                return null;
            }

            var user = new UserAccount
            {
                FullName = fullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = HashPassword(password),
                Phone = phone.Trim(),
                Role = normalizedRole,
                SelectedCampus = "ICTU",
                CreatedAt = DateTime.UtcNow.ToString("O"),
            };

            try
            {
                await _users.InsertAsync(user);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintError)
            {
                // Another registration with the same email won the race after EmailExistsAsync.
                _logger.LogWarning(ex, "Register rejected: email already in use (constraint).");
                return null;
            }

            // Sign the new user in right away, like most mobile apps do.
            CurrentUser = WithoutPasswordHash(user);
            _logger.LogInformation("User {UserId} registered with role {Role}.", user.Id, user.Role);
            return CurrentUser;
        }

        public void Logout()
        {
            if (CurrentUser is not null)
            {
                _logger.LogInformation("User {UserId} signed out.", CurrentUser.Id);
            }

            CurrentUser = null;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private static bool IsValidEmail(string email) =>
            MailAddress.TryCreate(email, out var address) && address.Address == email;

        /// <summary>
        /// Only Student and Landlord may self-register; Admin accounts come from the seed data.
        /// Returns the canonical casing from Constants.UserRoles, or null if not allowed.
        /// </summary>
        private static string? NormalizeSelfRegisterRole(string? role)
        {
            string[] allowed = [Constants.UserRoles.Student, Constants.UserRoles.Landlord];
            return allowed.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Returns a copy without the hash so the session never exposes it to the UI.</summary>
        private static UserAccount WithoutPasswordHash(UserAccount user) => new()
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PasswordHash = string.Empty,
            Phone = user.Phone,
            Role = user.Role,
            SelectedCampus = user.SelectedCampus,
            CreatedAt = user.CreatedAt,
        };

        internal static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{HashPrefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        internal static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            var parts = storedHash.Split('$');
            if (parts.Length == 4 && parts[0] == HashPrefix && int.TryParse(parts[1], out var iterations))
            {
                try
                {
                    var salt = Convert.FromBase64String(parts[2]);
                    var expected = Convert.FromBase64String(parts[3]);
                    var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                    return CryptographicOperations.FixedTimeEquals(actual, expected);
                }
                catch (FormatException)
                {
                    // Corrupted hash in the database: treat as "password does not match".
                    return false;
                }
            }

            // Legacy plain-text value: the demo users seeded by RentalDatabaseService (locked file)
            // store "123456" unhashed. Accounts created by RegisterAsync are always hashed.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(storedHash));
        }
    }
}
