using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public sealed record CampusOption(string Name, double Lat, double Lng);

public static class HomeFilterOptions
{
    public static readonly IReadOnlyList<CampusOption> Campuses =
    [
        new("Bách Khoa", 10.7721, 106.6579),
        new("KHTN", 10.7626, 106.6822)
    ];

    public static readonly IReadOnlyList<int> DistanceKilometres = [1, 2, 3, 5];

    public const int DefaultDistanceKm = 3;

    public static readonly IReadOnlyList<(string Label, RoomType? Value)> RoomTypes =
    [
        ("Tất cả", null),
        ("Phòng đơn", RoomType.SingleRoom),
        ("Ở ghép", RoomType.SharedRoom),
        ("Studio", RoomType.Studio),
        ("Căn hộ", RoomType.Apartment),
        ("Nhà", RoomType.House)
    ];
}
