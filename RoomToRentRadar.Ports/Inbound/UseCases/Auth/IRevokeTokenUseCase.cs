using RoomToRentRadar.Ports.Contracts.Requests.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface IRevokeTokenUseCase
{
    Task ExecuteAsync(string userId, RevokeTokenRequest request, CancellationToken ct = default);
}

