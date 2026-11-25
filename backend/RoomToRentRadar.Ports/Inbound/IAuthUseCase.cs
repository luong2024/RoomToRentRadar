using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Ports.Inbound;

public interface IAuthUseCase
{
    Task<LoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<AuthRegistrationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken);

    Task<User?> AuthenticateAsync(string token, CancellationToken cancellationToken);

    Task LogoutAsync(string token, CancellationToken cancellationToken);
}
