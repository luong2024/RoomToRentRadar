using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

public enum RoomType
{
    [JsonStringEnumMemberName("single_room")]
    SingleRoom,

    [JsonStringEnumMemberName("shared_room")]
    SharedRoom,

    [JsonStringEnumMemberName("studio")]
    Studio,

    [JsonStringEnumMemberName("apartment")]
    Apartment,

    [JsonStringEnumMemberName("house")]
    House
}

/// <summary>
/// Wire values for <see cref="RoomType"/>. Query parameters are parsed explicitly so
/// an unsupported value reports <c>invalid_room_type</c> instead of a binding error.
/// </summary>
public static class RoomTypeWire
{
    public const string SingleRoom = "single_room";
    public const string SharedRoom = "shared_room";
    public const string Studio = "studio";
    public const string Apartment = "apartment";
    public const string House = "house";

    /// <summary>Every supported wire value, in enum order.</summary>
    public static readonly IReadOnlyList<string> All =
        [SingleRoom, SharedRoom, Studio, Apartment, House];

    public static bool TryParse(string? value, out RoomType roomType)
    {
        switch (value)
        {
            case SingleRoom:
                roomType = RoomType.SingleRoom;
                return true;
            case SharedRoom:
                roomType = RoomType.SharedRoom;
                return true;
            case Studio:
                roomType = RoomType.Studio;
                return true;
            case Apartment:
                roomType = RoomType.Apartment;
                return true;
            case House:
                roomType = RoomType.House;
                return true;
            default:
                roomType = default;
                return false;
        }
    }

    public static string ToWireValue(RoomType roomType) => roomType switch
    {
        RoomType.SingleRoom => SingleRoom,
        RoomType.SharedRoom => SharedRoom,
        RoomType.Studio => Studio,
        RoomType.Apartment => Apartment,
        RoomType.House => House,
        _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null)
    };

    public static RoomType FromWireValue(string value)
        => TryParse(value, out var roomType)
            ? roomType
            : throw new InvalidOperationException($"Unknown room type '{value}'.");
}
