using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.SavedSearches;

public enum SavedSearchAlertState
{
    [JsonStringEnumMemberName("off")]
    Off,

    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("paused")]
    Paused
}
