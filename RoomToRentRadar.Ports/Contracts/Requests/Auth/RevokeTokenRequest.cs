namespace RoomToRentRadar.Ports.Contracts.Requests.Auth;

public record RevokeTokenRequest(
    string RefreshToken
);

