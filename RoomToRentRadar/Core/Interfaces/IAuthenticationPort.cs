using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// Inbound Application Port for authentication operations.
/// </summary>
public interface IAuthenticationPort
{
    Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default);
}
