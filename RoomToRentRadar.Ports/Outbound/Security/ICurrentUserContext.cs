namespace RoomToRentRadar.Ports.Outbound.Security;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
}
