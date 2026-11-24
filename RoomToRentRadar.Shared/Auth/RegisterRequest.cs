namespace RoomToRentRadar.Shared.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string Role);
