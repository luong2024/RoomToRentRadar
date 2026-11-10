namespace RoomToRentRadar.Ports.Contracts.Requests.Auth;

public record IctuSsoLoginRequest(
    string IdToken,
    string? DeviceInfo = null
);

