using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingCursorCodec
{
    public const int CurrentVersion = 2;

    private const int MaxCursorLength = 512;

    private sealed record Payload(int Version, string Sort, string Key, Guid Id, string Fingerprint);

    public static string Encode(
        ListingSort sort,
        Guid id,
        string filterFingerprint,
        DateTime? createdAt = null,
        long? price = null,
        double? areaM2 = null,
        double? distanceKm = null)
    {
        if (string.IsNullOrWhiteSpace(filterFingerprint))
        {
            throw new ArgumentException(
                "The filter fingerprint is required.", nameof(filterFingerprint));
        }

        var key = sort switch
        {
            ListingSort.Newest => createdAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ListingSort.PriceAsc or ListingSort.PriceDesc => price?.ToString(CultureInfo.InvariantCulture),
            ListingSort.AreaDesc => areaM2?.ToString("R", CultureInfo.InvariantCulture),
            ListingSort.DistanceAsc => distanceKm?.ToString("R", CultureInfo.InvariantCulture),
            _ => null
        };

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("A sort key is required for the selected sort.", nameof(sort));
        }

        var payload = new Payload(
            CurrentVersion,
            ListingSortWire.ToWireValue(sort),
            key,
            id,
            filterFingerprint);
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload, ApiJson.Options));
    }

    public static bool TryDecode(string? value, out ListingCursor cursor)
    {
        cursor = null!;

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxCursorLength)
        {
            return false;
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(
                Base64Url.DecodeFromChars(value), ApiJson.Options);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }

        if (payload is null
            || payload.Version != CurrentVersion
            || payload.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.Fingerprint)
            || !ListingSortWire.TryParse(payload.Sort, out var sort))
        {
            return false;
        }

        DateTime? createdAt = null;
        long? price = null;
        double? areaM2 = null;
        double? distanceKm = null;

        switch (sort)
        {
            case ListingSort.Newest:
                if (!DateTime.TryParseExact(
                        payload.Key,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var parsedCreatedAt))
                {
                    return false;
                }

                createdAt = parsedCreatedAt.ToUniversalTime();
                break;

            case ListingSort.PriceAsc:
            case ListingSort.PriceDesc:
                if (!long.TryParse(payload.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPrice))
                {
                    return false;
                }

                price = parsedPrice;
                break;

            case ListingSort.AreaDesc:
                if (!double.TryParse(payload.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedArea)
                    || double.IsNaN(parsedArea)
                    || double.IsInfinity(parsedArea))
                {
                    return false;
                }

                areaM2 = parsedArea;
                break;

            case ListingSort.DistanceAsc:
                if (!double.TryParse(payload.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDistance)
                    || double.IsNaN(parsedDistance)
                    || double.IsInfinity(parsedDistance))
                {
                    return false;
                }

                distanceKm = parsedDistance;
                break;

            default:
                return false;
        }

        cursor = new ListingCursor(
            sort,
            payload.Id,
            payload.Fingerprint,
            createdAt,
            price,
            areaM2,
            distanceKm);
        return true;
    }
}
