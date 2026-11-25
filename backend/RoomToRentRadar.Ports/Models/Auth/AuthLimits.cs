namespace RoomToRentRadar.Ports.Models.Auth;

/// <summary>Limits shared by the registration validator and the user EF configuration.</summary>
public static class AuthLimits
{
    public const int EmailMaxLength = 200;
    public const int DisplayNameMaxLength = 120;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int PasswordHashMaxLength = 200;
    public const int PasswordSaltMaxLength = 100;
    public const int OtpHashMaxLength = 200;
    public const int OtpSaltMaxLength = 100;
}
