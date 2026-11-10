using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Auth;

public sealed class GetCurrentUserProfileUseCase : IGetCurrentUserProfileUseCase
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserProfileUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<UserProfileDto> ExecuteAsync(string userId, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

