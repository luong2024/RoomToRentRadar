using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Models;

public sealed record MapSortOption(string Label, string WireValue)
{
    public static IReadOnlyList<MapSortOption> All { get; } =
    [
        new("Giá thấp nhất", ListingSortWire.PriceAsc),
        new("Gần khu vực nhất", ListingSortWire.DistanceAsc)
    ];
}
