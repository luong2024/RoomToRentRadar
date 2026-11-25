namespace RoomToRentRadar.Ports.Outbound.Repositories;

using RoomToRentRadar.Domain.Enums;

/// <summary>One row of the search projection; carries no domain behaviour (ADR-006 read model).</summary>
public sealed record RoomSearchCandidate(
    string Id,
    string Title,
    long MonthlyRentVnd,
    double AreaM2,
    RoomType RoomType,
    ListingStatus Status,
    bool IsVerified,
    // Storage key of the cover thumbnail (BR-MED-05); the use case turns it into a public URL.
    string? CoverThumbPath,
    double Latitude,
    double Longitude,
    DateTime? PublishedAtUtc,
    Furnishing? Furnishing = null
);

/// <summary>
/// Pre-filter pushed down to SQL. Bounding-box bounds are null when no
/// <c>maxDistanceKm</c> is requested (ADR-006).
/// </summary>
public sealed record RoomSearchQueryFilter(
    long? MinRentVnd,
    long? MaxRentVnd,
    double? MinAreaM2,
    double? MaxAreaM2,
    IReadOnlyList<RoomType>? RoomTypes,
    double? MinLat,
    double? MaxLat,
    double? MinLng,
    double? MaxLng
);

/// <summary>CQRS read model for US04 search (M2 / ADR-006).</summary>
public interface IRoomSearchReadModel
{
    Task<IReadOnlyList<RoomSearchCandidate>> QueryCandidatesAsync(RoomSearchQueryFilter filter, CancellationToken ct = default);
}
