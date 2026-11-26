namespace RoomToRentRadar.Shared.Auth;

public sealed record UserProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt);
