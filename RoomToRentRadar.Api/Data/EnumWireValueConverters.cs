using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Data;

public sealed class ListingStatusConverter : ValueConverter<ListingStatus, string>
{
    public ListingStatusConverter()
        : base(
            value => ToWireValue(value),
            value => FromWireValue(value))
    {
    }

    public static string ToWireValue(ListingStatus status) => status switch
    {
        ListingStatus.Draft => "draft",
        ListingStatus.Published => "published",
        ListingStatus.Hidden => "hidden",
        ListingStatus.Removed => "removed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ListingStatus FromWireValue(string value) => value switch
    {
        "draft" => ListingStatus.Draft,
        "published" => ListingStatus.Published,
        "hidden" => ListingStatus.Hidden,
        "removed" => ListingStatus.Removed,
        _ => throw new InvalidOperationException($"Unknown listing status '{value}'.")
    };
}

public sealed class RoomTypeConverter : ValueConverter<RoomType, string>
{
    public RoomTypeConverter()
        : base(
            value => RoomTypeWire.ToWireValue(value),
            value => RoomTypeWire.FromWireValue(value))
    {
    }
}

public sealed class ListingPhotoKindConverter : ValueConverter<ListingPhotoKind, string>
{
    public ListingPhotoKindConverter()
        : base(
            value => ToWireValue(value),
            value => FromWireValue(value))
    {
    }

    public static string ToWireValue(ListingPhotoKind kind) => kind switch
    {
        ListingPhotoKind.Photo => "photo",
        ListingPhotoKind.Panorama => "panorama",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static ListingPhotoKind FromWireValue(string value) => value switch
    {
        "photo" => ListingPhotoKind.Photo,
        "panorama" => ListingPhotoKind.Panorama,
        _ => throw new InvalidOperationException($"Unknown listing photo kind '{value}'.")
    };
}

public sealed class AmenityCodeConverter : ValueConverter<AmenityCode, string>
{
    public AmenityCodeConverter()
        : base(
            value => ToWireValue(value),
            value => FromWireValue(value))
    {
    }

    public static string ToWireValue(AmenityCode code) => code switch
    {
        AmenityCode.Wifi => "wifi",
        AmenityCode.AirConditioner => "air_conditioner",
        AmenityCode.Parking => "parking",
        AmenityCode.WashingMachine => "washing_machine",
        AmenityCode.Refrigerator => "refrigerator",
        AmenityCode.Kitchen => "kitchen",
        AmenityCode.PrivateBathroom => "private_bathroom",
        AmenityCode.Balcony => "balcony",
        AmenityCode.Elevator => "elevator",
        AmenityCode.Security => "security",
        AmenityCode.Gym => "gym",
        AmenityCode.Pool => "pool",
        AmenityCode.PetAllowed => "pet_allowed",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
    };

    public static AmenityCode FromWireValue(string value) => value switch
    {
        "wifi" => AmenityCode.Wifi,
        "air_conditioner" => AmenityCode.AirConditioner,
        "parking" => AmenityCode.Parking,
        "washing_machine" => AmenityCode.WashingMachine,
        "refrigerator" => AmenityCode.Refrigerator,
        "kitchen" => AmenityCode.Kitchen,
        "private_bathroom" => AmenityCode.PrivateBathroom,
        "balcony" => AmenityCode.Balcony,
        "elevator" => AmenityCode.Elevator,
        "security" => AmenityCode.Security,
        "gym" => AmenityCode.Gym,
        "pool" => AmenityCode.Pool,
        "pet_allowed" => AmenityCode.PetAllowed,
        _ => throw new InvalidOperationException($"Unknown amenity code '{value}'.")
    };
}
