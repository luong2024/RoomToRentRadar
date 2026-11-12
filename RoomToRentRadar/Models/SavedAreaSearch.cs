namespace RoomToRentRadar.Models;

public sealed record SavedAreaSearch(
    string Id,
    string Name,
    IReadOnlyList<GeoPoint> Vertices,
    DateTime SavedAt)
{
    public string DetailText => $"{Vertices.Count} đỉnh · lưu {SavedAt.ToLocalTime():dd/MM/yyyy HH:mm}";
}
