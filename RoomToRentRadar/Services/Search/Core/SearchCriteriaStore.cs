using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadar.Services.Search.Core;

public sealed class SearchCriteriaStore : ISearchCriteriaStore
{
    private readonly object _gate = new();
    private RoomSearchCriteria _current = RoomSearchCriteria.Default;

    public RoomSearchCriteria Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public event EventHandler<RoomSearchCriteria>? CriteriaChanged;

    public void Apply(RoomSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        lock (_gate)
        {
            if (_current.IsEquivalentTo(criteria))
                return;

            _current = criteria;
        }

        CriteriaChanged?.Invoke(this, criteria);
    }

    public void ClearFilters() => Apply(Current.ClearFiltersPreservingCampus());

    public void RemoveChip(ActiveFilterChipKind kind, RoomType? roomType = null) =>
        Apply(Current.RemoveChip(kind, roomType));

    public void SetSort(string? sort)
    {
        var normalized =
            string.IsNullOrWhiteSpace(sort) ||
            string.Equals(sort, RoomSearchRequest.DefaultSort, StringComparison.Ordinal)
                ? null
                : sort;

        Apply(Current with { Sort = normalized });
    }

    public void SetCampus(string? campusId, string campusDisplayName) =>
        Apply(Current with { CampusId = campusId, CampusDisplayName = campusDisplayName });
}
