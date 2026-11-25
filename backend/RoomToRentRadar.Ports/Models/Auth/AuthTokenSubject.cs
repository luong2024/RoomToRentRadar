using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Models.Auth;

public sealed record AuthTokenSubject(
    Guid UserId,
    string Email,
    string DisplayName,
    UserRole Role);
