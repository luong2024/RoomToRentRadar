using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.UseCases.Authentication;

public sealed class AccountSecurityValidator : IAccountSecurityValidator
{
    private readonly IUserRepository _userRepository;

    public AccountSecurityValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> ValidateTokenSecurityStateAsync(
        Guid userId,
        string securityStamp,
        string? roleClaim,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(securityStamp) || string.IsNullOrWhiteSpace(roleClaim))
        {
            return false;
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (!user.ValidateSecurityState(securityStamp))
        {
            return false;
        }

        return string.Equals(user.Role.ToRoleName(), roleClaim, StringComparison.Ordinal);
    }
}
