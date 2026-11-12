namespace RoomToRentRadar.Models;

public readonly record struct GeoPoint(double Lat, double Lng)
{
    public bool IsValid => double.IsFinite(Lat) && double.IsFinite(Lng)
        && Lat is >= -90 and <= 90 && Lng is >= -180 and <= 180;
}
