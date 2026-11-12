namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

/// <summary>
/// Driven port for accessing the security context of the current caller.
/// </summary>
public interface ICurrentUserContextPort
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
}
