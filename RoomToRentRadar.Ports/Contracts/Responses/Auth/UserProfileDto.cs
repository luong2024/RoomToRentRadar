namespace RoomToRentRadar.Ports.Contracts.Responses.Auth;

public record UserProfileDto(
    string Id,
    string Email,
    string FullName,
    string Role,
    string AuthProvider,
    string? PhoneNumber,
    string? StudentCode,
    bool IsVerified,
    DateTime CreatedAtUtc
);

