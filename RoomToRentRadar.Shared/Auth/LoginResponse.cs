namespace RoomToRentRadar.Shared.Auth;

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, string DisplayName);
