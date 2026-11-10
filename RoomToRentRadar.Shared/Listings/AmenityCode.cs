using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

public enum AmenityCode
{
    [JsonStringEnumMemberName("wifi")]
    Wifi,

    [JsonStringEnumMemberName("air_conditioner")]
    AirConditioner,

    [JsonStringEnumMemberName("parking")]
    Parking,

    [JsonStringEnumMemberName("washing_machine")]
    WashingMachine,

    [JsonStringEnumMemberName("refrigerator")]
    Refrigerator,

    [JsonStringEnumMemberName("kitchen")]
    Kitchen,

    [JsonStringEnumMemberName("private_bathroom")]
    PrivateBathroom,

    [JsonStringEnumMemberName("balcony")]
    Balcony,

    [JsonStringEnumMemberName("elevator")]
    Elevator,

    [JsonStringEnumMemberName("security")]
    Security,

    [JsonStringEnumMemberName("gym")]
    Gym,

    [JsonStringEnumMemberName("pool")]
    Pool,

    [JsonStringEnumMemberName("pet_allowed")]
    PetAllowed
}
