using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

public enum ListingPhotoKind
{
    [JsonStringEnumMemberName("photo")]
    Photo,

    [JsonStringEnumMemberName("panorama")]
    Panorama
}
