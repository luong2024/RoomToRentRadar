using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Infrastructure.Persistence.Seed;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Infrastructure.Fakes
{
    /// <summary>
    /// In-memory fallback implementation of <see cref="IAuthService"/> registered via <c>TryAddSingleton</c>.
    /// Automatically replaced as soon as T01.1 registers the real SQLite-backed <c>AuthService</c>.
    /// </summary>
    public sealed class InMemoryAuthService : IAuthService
    {
        private readonly List<UserAccount> _users = IctuSeedData.Users();

        public UserAccount? CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser is not null;

        public Task<UserAccount?> LoginAsync(string email, string password)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == password);
            CurrentUser = user;
            return Task.FromResult(user);
        }

        public Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                _users.Any(u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult<UserAccount?>(null);
            }

            var account = new UserAccount
            {
                Id = _users.Count == 0 ? 1 : _users.Max(u => u.Id) + 1,
                FullName = fullName.Trim(),
                Email = email.Trim(),
                PasswordHash = password,
                Phone = phone.Trim(),
                Role = string.IsNullOrWhiteSpace(role) ? Constants.UserRoles.Student : role,
                SelectedCampus = "ICTU",
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            _users.Add(account);
            CurrentUser = account;
            return Task.FromResult<UserAccount?>(account);
        }

        public void Logout() => CurrentUser = null;
    }
}
