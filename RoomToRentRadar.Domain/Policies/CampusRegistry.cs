namespace RoomToRentRadar.Domain.Policies;

/// <summary>
/// Authoritative registry of campus geographic coordinates per PRD §4.1 (ERD §4.1) and BR-CMP-03.
/// This is the single source of truth for all campus location data in the system.
/// Any feature that needs campus coordinates (search, detail, similar-rooms) must use this class
/// instead of maintaining its own copy.
/// </summary>
public static class CampusRegistry
{
    /// <summary>The campus ID used when no campusId is provided by the caller.</summary>
    public const string DefaultCampusId = "ICTU";

    private static readonly IReadOnlyDictionary<string, (double Lat, double Lng)> Coordinates =
        new Dictionary<string, (double Lat, double Lng)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ICTU"]  = (21.5851,  105.8066),
            ["TNUS"]  = (21.5887,  105.80979),
            ["SFL"]   = (21.5866,  105.8116),
            ["TUEBA"] = (21.5937,  105.8048),
            ["TUAF"]  = (21.5794,  105.8277),
            ["TNUE"]  = (21.5822,  105.8170)
        };

    /// <summary>
    /// Tries to retrieve the geographic coordinates for a given campus ID.
    /// Comparison is case-insensitive (e.g. "ictu", "ICTU", "Ictu" all match).
    /// </summary>
    /// <param name="campusId">The campus identifier to look up.</param>
    /// <param name="coords">The coordinates tuple if found.</param>
    /// <returns>True if the campus was found; false otherwise.</returns>
    public static bool TryGetCoordinates(string campusId, out (double Lat, double Lng) coords)
        => Coordinates.TryGetValue(campusId, out coords);

    /// <summary>
    /// Returns the coordinates for the default campus (ICTU).
    /// Used as the fallback when no campusId is specified by the caller.
    /// </summary>
    public static (double Lat, double Lng) GetDefaultCoordinates()
        => Coordinates[DefaultCampusId];

    /// <summary>
    /// Returns true if the given campus ID exists in the registry.
    /// </summary>
    public static bool IsKnownCampus(string campusId)
        => !string.IsNullOrWhiteSpace(campusId) && Coordinates.ContainsKey(campusId);

    /// <summary>Read-only view of all registered campuses (for diagnostics / seeding).</summary>
    public static IReadOnlyDictionary<string, (double Lat, double Lng)> All => Coordinates;
}
