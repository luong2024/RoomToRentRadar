namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Room types supported by RoomToRentRadar per docs/erd.md §4.4 and docs/api/openapi.yaml.
/// </summary>
public enum RoomType
{
    BoardingRoom = 0,
    MiniApartment = 1,
    WholeHouse = 2,
    Dormitory = 3
}
