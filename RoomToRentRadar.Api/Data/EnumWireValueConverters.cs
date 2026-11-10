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
