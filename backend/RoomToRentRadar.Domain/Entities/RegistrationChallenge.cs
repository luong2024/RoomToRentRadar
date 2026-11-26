using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Domain.Entities;

public sealed class RegistrationChallenge
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public string OtpSalt { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
