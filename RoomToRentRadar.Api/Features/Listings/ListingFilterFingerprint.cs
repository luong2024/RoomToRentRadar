using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Stable fingerprint of the filter set a cursor belongs to. The cursor carries it
/// so reusing a cursor after changing filters is rejected instead of silently
/// skipping rows.
/// </summary>
public static class ListingFilterFingerprint
{
    public static string Compute(ListingFilters filters)
    {
        var canonical = string.Join(
            '|',
            filters.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            filters.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Format(filters.MinAreaM2),
            Format(filters.MaxAreaM2),
            filters.Bedrooms?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            filters.RoomType is { } roomType ? RoomTypeWire.ToWireValue(roomType) : string.Empty,
            Format(filters.Campus?.Lat),
            Format(filters.Campus?.Lng),
            Format(filters.Campus?.MaxDistanceKm));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Format(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
}
