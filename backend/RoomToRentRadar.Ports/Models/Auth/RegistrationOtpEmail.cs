namespace RoomToRentRadar.Ports.Models.Auth;

public sealed record RegistrationOtpEmail(
    string ToEmail,
    string DisplayName,
    string Code,
    DateTime ExpiresAt);
