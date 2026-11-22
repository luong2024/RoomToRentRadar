using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.UseCases.Moderation;

public static class ModerationListingQueueCursorCodec
{
    public const int CurrentVersion = 1;

    private const int MaxCursorLength = 512;

    private sealed record Payload(int Version, string Status, string CreatedAt, Guid ListingId);

    public static string Encode(ListingStatus status, DateTime createdAt, Guid listingId)
    {
        var payload = new Payload(
            CurrentVersion,
            ListingStatusWire.ToWireValue(status),
            createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            listingId);
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload, ApiJson.Options));
    }

    public static bool TryDecode(string? value, out ModerationListingQueueCursor cursor)
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
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return false;
        }

        if (payload is null
            || payload.Version != CurrentVersion
            || !ListingStatusWire.TryParse(payload.Status, out var status)
            || payload.ListingId == Guid.Empty
            || !DateTime.TryParseExact(
                payload.CreatedAt,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdAt))
        {
            return false;
        }

        cursor = new ModerationListingQueueCursor(
            status,
            createdAt.ToUniversalTime(),
            payload.ListingId);
        return true;
    }
}
