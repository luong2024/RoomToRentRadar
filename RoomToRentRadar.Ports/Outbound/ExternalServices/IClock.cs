namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

/// <summary>Testable UTC clock abstraction (<c>docs/system-architecture.md</c> §6.3).</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
