using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryCurrentUserContext : ICurrentUserContextPort
{
    public bool IsAuthenticated { get; set; }
    public string? UserId { get; set; }

    public InMemoryCurrentUserContext(bool isAuthenticated = false, string? userId = null)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
    }
}
