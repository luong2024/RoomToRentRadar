namespace RoomToRentRadar.Shared.Dtos;

public sealed record GeoJsonPointDto(string Type, double[] Coordinates)
{
    public static GeoJsonPointDto FromLngLat(double longitude, double latitude)
        => new("Point", [longitude, latitude]);
}
