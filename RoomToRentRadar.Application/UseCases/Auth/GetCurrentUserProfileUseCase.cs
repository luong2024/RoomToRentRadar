using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Exceptions;
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

    public async Task<UserProfileDto> ExecuteAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainValidationException("User ID is required.");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        return UserMapper.ToProfileDto(user);
    }
}
