namespace RoomToRentRadar.Domain.Policies;

/// <summary>
/// Domain policy and constants for similar room recommendations per docs/business-analysis.md BR-LST-10.
/// </summary>
public static class SimilarRoomsPolicy
{
    /// <summary>
    /// Maximum number of similar rooms returned per BR-LST-10 and OpenAPI schema.
    /// </summary>
    public const int MaxSimilarRooms = 5;

    /// <summary>
    /// Search radius around the target room pin in kilometers (2 km per BR-LST-10).
    /// </summary>
    public const double MaxDistanceKm = 2.0;

    /// <summary>
    /// Rent tolerance ratio (±20% per BR-LST-10).
    /// </summary>
    public const decimal PriceToleranceRatio = 0.20m;

    /// <summary>
    /// Minimum result count required before relaxing room type criteria (BR-LST-10).
    /// </summary>
    public const int MinResultsBeforeRelaxingRoomType = 2;
}
