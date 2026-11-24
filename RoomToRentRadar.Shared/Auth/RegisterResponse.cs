namespace RoomToRentRadar.Shared.Auth;

public sealed record RegisterResponse(Guid UserId, string Email, string DisplayName, string Role);
