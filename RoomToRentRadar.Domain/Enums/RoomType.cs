namespace RoomToRentRadar.Domain.Enums;

/// <summary>Room types allowed by <c>docs/erd.md</c> §4.4 and <c>docs/api/openapi.yaml</c>.</summary>
public enum RoomType
{
    BoardingRoom = 1,
    MiniApartment = 2,
    WholeHouse = 3,
    Dormitory = 4
}
