namespace RoomToRentRadar.Ports.Contracts.Requests.Auth;

public record RegisterUserRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    string? PhoneNumber = null,
    string? StudentCode = null
);

