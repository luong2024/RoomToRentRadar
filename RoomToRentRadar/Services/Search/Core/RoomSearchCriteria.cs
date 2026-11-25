using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadar.Services.Search.Core;

/// <summary>
/// Immutable client-side search criteria shared by Explore and the Filters panel (US04, BR-SRCH-01..06).
/// <see cref="Sort"/> = <c>null</c> means the server default ("distance"), so the cleared criteria map
/// exactly to <c>new RoomSearchRequest()</c> (BR-SRCH-06).
/// </summary>
public sealed record RoomSearchCriteria
{
    public static RoomSearchCriteria Default { get; } = new();

    public string? CampusId { get; init; }

    public string CampusDisplayName { get; init; } = "ICTU";

    public long? MinRentVnd { get; init; }

    public long? MaxRentVnd { get; init; }

    public double? MinAreaM2 { get; init; }

    public double? MaxAreaM2 { get; init; }

    public IReadOnlyList<RoomType> RoomTypes { get; init; } = Array.Empty<RoomType>();

    public double? MaxDistanceKm { get; init; }

    public string? Sort { get; init; }

    public string EffectiveSort =>
        string.IsNullOrWhiteSpace(Sort) ? RoomSearchRequest.DefaultSort : Sort;

    public bool HasPriceFilter => MinRentVnd.HasValue || MaxRentVnd.HasValue;

    public bool HasAreaFilter => MinAreaM2.HasValue || MaxAreaM2.HasValue;

    // Sort is not counted: it renders on its own button, not in the active-filter badge (US04 AC3).
    public int ActiveFilterCount =>
        (HasPriceFilter ? 1 : 0) +
        (HasAreaFilter ? 1 : 0) +
        RoomTypes.Count +
        (MaxDistanceKm.HasValue ? 1 : 0);

    public bool HasActiveFilters => ActiveFilterCount > 0;

    /// <summary>BR-SRCH-06: keep the campus, drop every filter, sort back to the default.</summary>
    public RoomSearchCriteria ClearFiltersPreservingCampus() =>
        this with
        {
            MinRentVnd = null,
            MaxRentVnd = null,
            MinAreaM2 = null,
            MaxAreaM2 = null,
            RoomTypes = Array.Empty<RoomType>(),
            MaxDistanceKm = null,
            Sort = null
        };

    public RoomSearchCriteria RemoveChip(ActiveFilterChipKind kind, RoomType? roomType = null) =>
        kind switch
        {
            ActiveFilterChipKind.Price => this with { MinRentVnd = null, MaxRentVnd = null },
            ActiveFilterChipKind.Area => this with { MinAreaM2 = null, MaxAreaM2 = null },
            ActiveFilterChipKind.RoomType when roomType.HasValue =>
                this with { RoomTypes = RoomTypes.Where(t => t != roomType.Value).ToArray() },
            ActiveFilterChipKind.MaxDistance => this with { MaxDistanceKm = null },
            _ => this
        };

    public RoomSearchRequest ToRoomSearchRequest(int pageNumber = 1, int pageSize = 20) => new(
        CampusId,
        MinRentVnd,
        MaxRentVnd,
        MinAreaM2,
        MaxAreaM2,
        RoomTypes.Count == 0 ? null : RoomTypes.ToArray(),
        MaxDistanceKm,
        // "distance" is the server default; send null so the cleared state equals new RoomSearchRequest().
        string.IsNullOrWhiteSpace(Sort) || string.Equals(Sort, RoomSearchRequest.DefaultSort, StringComparison.Ordinal)
            ? null
            : Sort,
        pageNumber,
        pageSize);

    /// <summary>
    /// Value equality for the store's skip-if-unchanged check; the record-generated equality would
    /// compare <see cref="RoomTypes"/> by reference.
    /// </summary>
    public bool IsEquivalentTo(RoomSearchCriteria? other)
    {
        if (other is null)
            return false;

        return string.Equals(CampusId, other.CampusId, StringComparison.Ordinal) &&
               string.Equals(CampusDisplayName, other.CampusDisplayName, StringComparison.Ordinal) &&
               MinRentVnd == other.MinRentVnd &&
               MaxRentVnd == other.MaxRentVnd &&
               Nullable.Equals(MinAreaM2, other.MinAreaM2) &&
               Nullable.Equals(MaxAreaM2, other.MaxAreaM2) &&
               Nullable.Equals(MaxDistanceKm, other.MaxDistanceKm) &&
               string.Equals(Sort, other.Sort, StringComparison.Ordinal) &&
               RoomTypes.OrderBy(t => t).SequenceEqual(other.RoomTypes.OrderBy(t => t));
    }
}
