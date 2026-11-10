using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

public enum ListingStatus
{
    [JsonStringEnumMemberName("draft")]
    Draft,

    [JsonStringEnumMemberName("published")]
    Published,

    [JsonStringEnumMemberName("hidden")]
    Hidden,

    [JsonStringEnumMemberName("removed")]
    Removed
}
