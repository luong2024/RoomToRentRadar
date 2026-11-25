using RoomToRentRadar.Models;

namespace RoomToRentRadar.Abstractions.Auth
{
    /// <summary>
    /// Contract for US01 Authentication &amp; User Session.
    /// Implemented in T01.1 (Infrastructure/Features/Auth). Consumed in T01.2, T03.2, T09.1.
    /// </summary>
    public interface IAuthService
    {
        UserAccount? CurrentUser { get; }
        bool IsSignedIn { get; }
        Task<UserAccount?> LoginAsync(string email, string password);
        Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role);
        void Logout();
    }
}
