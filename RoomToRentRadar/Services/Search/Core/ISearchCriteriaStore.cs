using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Services.Search.Core;

/// <summary>
/// Single source of truth for the current search criteria, shared between Explore (SCR-HOME-01)
/// and the Filters panel (SCR-SRCH-01). Registered as a DI singleton; subscribers must use weak
/// references so the singleton never keeps a PageModel alive.
/// </summary>
public interface ISearchCriteriaStore
{
    RoomSearchCriteria Current { get; }

    event EventHandler<RoomSearchCriteria>? CriteriaChanged;

    void Apply(RoomSearchCriteria criteria);

    void ClearFilters();

    void RemoveChip(ActiveFilterChipKind kind, RoomType? roomType = null);

    void SetSort(string? sort);

    void SetCampus(string? campusId, string campusDisplayName);
}
