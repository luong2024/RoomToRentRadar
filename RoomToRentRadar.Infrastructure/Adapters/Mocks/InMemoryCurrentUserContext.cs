using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryCurrentUserContext : ICurrentUserContext
{
    public bool IsAuthenticated { get; set; }
    public string? UserId { get; set; }

    public InMemoryCurrentUserContext(bool isAuthenticated = false, string? userId = null)
    {
        IsAuthenticated = isAuthenticated;
        UserId = userId;
    }
}
