namespace RoomToRentRadar.Ports.Contracts.Requests.Auth;

public record GoogleLoginRequest(
    string IdToken,
    string Role,
    string? PhoneNumber = null,
    string? DeviceInfo = null
);

