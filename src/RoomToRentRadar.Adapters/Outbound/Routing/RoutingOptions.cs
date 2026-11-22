namespace RoomToRentRadar.Adapters.Outbound.Routing;

public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    public string BaseUrl { get; init; } = "https://router.project-osrm.org/";

    public string Profile { get; init; } = "driving";

    public int TimeoutSeconds { get; init; } = 10;
}
