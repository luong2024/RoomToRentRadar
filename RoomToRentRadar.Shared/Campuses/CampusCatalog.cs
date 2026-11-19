namespace RoomToRentRadar.Shared.Campuses;

public sealed record CampusInfo(string Id, string Name, double Lat, double Lng);

public static class CampusCatalog
{
    public static readonly IReadOnlyList<CampusInfo> All =
    [
        new("bach-khoa", "Bách Khoa", 10.7721, 106.6579),
        new("khtn", "KHTN", 10.7626, 106.6822)
    ];

    public static CampusInfo? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalized = id.Trim();
        return All.FirstOrDefault(campus =>
            string.Equals(campus.Id, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static CampusInfo? FindByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim();
        return All.FirstOrDefault(campus =>
            string.Equals(campus.Name, normalized, StringComparison.Ordinal));
    }
}
