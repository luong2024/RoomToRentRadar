using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadar.Api.Data;

public sealed class ListingStatusConverter : ValueConverter<ListingStatus, string>
{
    public ListingStatusConverter()
        : base(
            value => ToWireValue(value),
            value => FromWireValue(value))
    {
    }

    public static string ToWireValue(ListingStatus status) => ListingStatusWire.ToWireValue(status);

    public static ListingStatus FromWireValue(string value) => ListingStatusWire.FromWireValue(value);
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

public sealed class SavedSearchAlertStateConverter : ValueConverter<SavedSearchAlertState, string>
{
    public SavedSearchAlertStateConverter()
        : base(
            value => ToWireValue(value),
            value => FromWireValue(value))
    {
    }

    public static string ToWireValue(SavedSearchAlertState state) => state switch
    {
        SavedSearchAlertState.Off => "off",
        SavedSearchAlertState.Active => "active",
        SavedSearchAlertState.Paused => "paused",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    public static SavedSearchAlertState FromWireValue(string value) => value switch
    {
        "off" => SavedSearchAlertState.Off,
        "active" => SavedSearchAlertState.Active,
        "paused" => SavedSearchAlertState.Paused,
        _ => throw new InvalidOperationException($"Unknown saved search alert state '{value}'.")
    };
}

public sealed class UserRoleConverter : ValueConverter<UserRole, string>
{
    public UserRoleConverter()
        : base(
            value => UserRoleWire.ToWireValue(value),
            value => UserRoleWire.FromWireValue(value))
    {
    }
}

public sealed class ListingReportReasonConverter : ValueConverter<ListingReportReason, string>
{
    public ListingReportReasonConverter()
        : base(
            value => ListingReportReasonWire.ToWireValue(value),
            value => ListingReportReasonWire.FromWireValue(value))
    {
    }
}

public sealed class ListingReportStatusConverter : ValueConverter<ListingReportStatus, string>
{
    public ListingReportStatusConverter()
        : base(
            value => ListingReportStatusWire.ToWireValue(value),
            value => ListingReportStatusWire.FromWireValue(value))
    {
    }
}

public sealed class ModerationAuditActionConverter : ValueConverter<ModerationAuditAction, string>
{
    public ModerationAuditActionConverter()
        : base(
            value => ModerationAuditActionWire.ToWireValue(value),
            value => ModerationAuditActionWire.FromWireValue(value))
    {
    }
}

public sealed class ModerationResolutionActionConverter : ValueConverter<ModerationResolutionAction, string>
{
    public ModerationResolutionActionConverter()
        : base(
            value => ModerationResolutionActionWire.ToWireValue(value),
            value => ModerationResolutionActionWire.FromWireValue(value))
    {
    }
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
