using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Ports.Inbound;

public interface IAuthUseCase
{
    Task<BeginRegistrationResult> BeginRegistrationAsync(
        BeginRegistrationCommand command,
        CancellationToken cancellationToken);

    Task<VerifyRegistrationResult> VerifyRegistrationAsync(
        VerifyRegistrationCommand command,
        CancellationToken cancellationToken);

    Task<LoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<User?> AuthenticateAsync(string token, CancellationToken cancellationToken);

    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileDto?> UpdateDisplayNameAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken);

    Task LogoutAsync(string token, CancellationToken cancellationToken);
}
