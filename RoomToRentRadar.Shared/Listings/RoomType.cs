using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

public enum RoomType
{
    [JsonStringEnumMemberName("studio")]
    Studio,

    [JsonStringEnumMemberName("apartment")]
    Apartment,

    [JsonStringEnumMemberName("house")]
    House,

    [JsonStringEnumMemberName("shared_room")]
    SharedRoom
}
