namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

/// <summary>
/// Provides identity and authorization context for the current caller (BR-AUTH-07).
/// Allows application layer to determine authentication state without referencing ASP.NET HttpContext.
/// </summary>
public interface ICurrentUserContextPort
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? Role { get; }
}
