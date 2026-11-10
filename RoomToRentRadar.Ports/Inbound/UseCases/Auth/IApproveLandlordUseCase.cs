using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Auth;

public interface IApproveLandlordUseCase
{
    Task<UserProfileDto> ExecuteAsync(string adminUserId, ApproveLandlordRequest request, CancellationToken ct = default);
}

