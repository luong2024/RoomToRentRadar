using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Ports.Models.Auth;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName,
    UserRole Role);

public enum RegisterUserOutcome
{
    Created,
    EmailAlreadyRegistered
}

public sealed record RegisterUserResult(
    RegisterUserOutcome Outcome,
    RegisterResponse? Response)
{
    public static RegisterUserResult Created(RegisterResponse response)
        => new(RegisterUserOutcome.Created, response);

    public static RegisterUserResult DuplicateEmail()
        => new(RegisterUserOutcome.EmailAlreadyRegistered, null);
}
