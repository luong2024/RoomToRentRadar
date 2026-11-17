using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadar.Models;

public sealed record SavedAreaSearch(
    string Id,
    string Name,
    IReadOnlyList<GeoPoint> Vertices,
    DateTime SavedAt)
{
    public const int MaximumNameLength = 60;
    public const int MaximumCount = 20;
    public const int MaximumVertices = 200;

    public string Sort { get; init; } = ListingSortWire.DistanceAsc;

    /// <summary>Server-side saved search created when notifications are enabled.</summary>
    public Guid? RemoteId { get; init; }

    public SavedSearchAlertState AlertState { get; init; } = SavedSearchAlertState.Off;

    [JsonIgnore]
    public string AlertStateText => AlertState switch
    {
        SavedSearchAlertState.Active => "Đang nhận thông báo phòng mới.",
        SavedSearchAlertState.Paused => "Thông báo đang tạm dừng.",
        _ => "Bật để nhận thông báo khi có phòng mới trong vùng."
    };

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Name) && Name.Length <= MaximumNameLength
        && Vertices is { Count: >= PolygonDraft.MinimumVertices and <= MaximumVertices }
        && Vertices.All(vertex => vertex.IsValid)
        && Vertices.Distinct().Count() == Vertices.Count
        && HasValidGeometry()
        && Sort is ListingSortWire.PriceAsc or ListingSortWire.DistanceAsc;

    [JsonIgnore]
    public string DetailText => $"{Vertices.Count} đỉnh · lưu {SavedAt.ToLocalTime():dd/MM/yyyy HH:mm}";

    private bool HasValidGeometry()
    {
        var coordinates = Vertices.Select(vertex => new Coordinate(vertex.Lng, vertex.Lat))
            .Append(new Coordinate(Vertices[0].Lng, Vertices[0].Lat)).ToArray();
        var polygon = new GeometryFactory().CreatePolygon(coordinates);
        return polygon.IsValid && polygon.Area > 0;
    }
}
