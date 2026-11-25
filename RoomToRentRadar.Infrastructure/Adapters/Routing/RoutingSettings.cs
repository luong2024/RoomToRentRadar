namespace RoomToRentRadar.Infrastructure.Adapters.Routing;

/// <summary>
/// OpenRouteService routing configuration (ADR-007, <c>docs/system-architecture.md</c> §7.5).
/// <see cref="UseMock"/> MUST remain <c>false</c> in root <c>appsettings.json</c>.
/// </summary>
public sealed class RoutingSettings
{
    public const string SectionName = "Routing";
    public const int DefaultTimeoutSeconds = 3;

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org";

    public string? OpenRouteServiceApiKey { get; set; }

    public bool UseMock { get; set; } = false;

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
}
