using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Ports.Outbound.Security;

/// <summary>
/// Alias for ICurrentUserContextPort for backwards compatibility.
/// Preferred interface is ICurrentUserContextPort under Outbound/ExternalServices.
/// </summary>
public interface ICurrentUserContext : ICurrentUserContextPort
{
}
