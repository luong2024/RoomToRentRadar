using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Ports.Models.Auth;

public sealed record BeginRegistrationCommand(
    string Email,
    string Password,
    string DisplayName,
    UserRole Role);

public enum BeginRegistrationOutcome
{
    ChallengeCreated,
    EmailAlreadyRegistered,
    RoleNotAllowed,
    EmailDeliveryFailed
}

public sealed record BeginRegistrationResult(
    BeginRegistrationOutcome Outcome,
    RegisterChallengeResponse? Response)
{
    public static BeginRegistrationResult ChallengeCreated(RegisterChallengeResponse response)
        => new(BeginRegistrationOutcome.ChallengeCreated, response);

    public static BeginRegistrationResult DuplicateEmail()
        => new(BeginRegistrationOutcome.EmailAlreadyRegistered, null);

    public static BeginRegistrationResult RoleNotAllowed()
        => new(BeginRegistrationOutcome.RoleNotAllowed, null);

    public static BeginRegistrationResult EmailDeliveryFailed()
        => new(BeginRegistrationOutcome.EmailDeliveryFailed, null);
}
