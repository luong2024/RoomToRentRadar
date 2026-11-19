using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface IGetCurrentUserProfileUseCase
{
    Task<UserProfileDto> ExecuteAsync(string userId, CancellationToken ct = default);
}

