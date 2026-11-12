namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Amenity catalog per docs/erd.md §4.5 and docs/api/openapi.yaml.
/// </summary>
public enum Amenity
{
    AirConditioner = 0,
    WaterHeater = 1,
    PrivateBathroom = 2,
    Wifi = 3,
    Kitchen = 4,
    WashingMachine = 5,
    Fridge = 6,
    Bed = 7,
    Wardrobe = 8,
    Desk = 9,
    Balcony = 10,
    Window = 11,
    MotorbikeParking = 12,
    Elevator = 13,
    DryingArea = 14
}
