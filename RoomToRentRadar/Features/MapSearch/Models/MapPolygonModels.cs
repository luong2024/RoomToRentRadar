namespace RoomToRentRadar.Features.MapSearch.Models;

/// <summary>
/// Represents a geographical coordinate (latitude, longitude) on the map.
/// </summary>
public record GeoCoordinate(double Latitude, double Longitude);

/// <summary>
/// Represents an editable anchor pin that can be dragged to adjust polygon vertices in edit mode.
/// </summary>
public class EditableAnchorPin
{
    public int Index { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float ScreenX { get; set; }
    public float ScreenY { get; set; }

    public EditableAnchorPin(int index, double latitude, double longitude, float screenX = 0, float screenY = 0)
    {
        Index = index;
        Latitude = latitude;
        Longitude = longitude;
        ScreenX = screenX;
        ScreenY = screenY;
    }
}

/// <summary>
/// Represents a drawn polygon area enclosed by a list of geo-coordinates.
/// </summary>
public class DrawnPolygonArea
{
    public List<GeoCoordinate> Vertices { get; set; } = new();

    public bool IsValid => Vertices.Count >= 3;

    public void Clear()
    {
        Vertices.Clear();
    }
}
