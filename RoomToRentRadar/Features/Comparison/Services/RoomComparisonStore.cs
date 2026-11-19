using RoomToRentRadar.Features.Comparison.Models;

namespace RoomToRentRadar.Features.Comparison.Services;

public enum RoomComparisonAddResult
{
    Added,
    AlreadyCompared,
    LimitReached
}

public interface IRoomComparisonStore
{
    int MaxRooms { get; }

    IReadOnlyList<ComparisonCandidate> Rooms { get; }

    bool Contains(Guid listingId);

    RoomComparisonAddResult TryAdd(ComparisonCandidate candidate);

    bool Remove(Guid listingId);

    void Clear();
}

public sealed class RoomComparisonStore : IRoomComparisonStore
{
    public const int ComparisonLimit = 3;

    private readonly List<ComparisonCandidate> _rooms = new();

    public int MaxRooms => ComparisonLimit;

    public IReadOnlyList<ComparisonCandidate> Rooms => _rooms.ToArray();

    public bool Contains(Guid listingId) => _rooms.Exists(room => room.Id == listingId);

    public RoomComparisonAddResult TryAdd(ComparisonCandidate candidate)
    {
        if (Contains(candidate.Id))
        {
            return RoomComparisonAddResult.AlreadyCompared;
        }

        if (_rooms.Count >= ComparisonLimit)
        {
            return RoomComparisonAddResult.LimitReached;
        }

        _rooms.Add(candidate);
        return RoomComparisonAddResult.Added;
    }

    public bool Remove(Guid listingId) => _rooms.RemoveAll(room => room.Id == listingId) > 0;

    public void Clear() => _rooms.Clear();
}
