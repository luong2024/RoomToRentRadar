namespace RoomToRentRadar.Features.MapSearch.Models;

/// <summary>
/// Đại diện cho một điểm toạ độ địa lý (kinh độ, vĩ độ)
/// </summary>
public record GeoCoordinate(double Latitude, double Longitude);

/// <summary>
/// Điểm neo có thể kéo thả để tinh chỉnh đa giác ở chế độ Edit
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
/// Đại diện cho vùng đa giác đã vẽ và trạng thái khép kín
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
