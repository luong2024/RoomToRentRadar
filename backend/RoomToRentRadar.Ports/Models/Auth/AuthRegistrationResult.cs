using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Ports.Models.Auth;

public enum AuthRegistrationStatus
{
    Success,
    EmailAlreadyRegistered,
    UnsupportedRole
}

public sealed record AuthRegistrationResult(AuthRegistrationStatus Status, LoginResponse? Response)
{
    public bool IsSuccess => Status == AuthRegistrationStatus.Success;

    public static AuthRegistrationResult Registered(LoginResponse response)
        => new(AuthRegistrationStatus.Success, response);

    public static AuthRegistrationResult EmailAlreadyRegistered()
        => new(AuthRegistrationStatus.EmailAlreadyRegistered, null);

    public static AuthRegistrationResult UnsupportedRole()
        => new(AuthRegistrationStatus.UnsupportedRole, null);
}
