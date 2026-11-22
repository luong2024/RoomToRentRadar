using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.UseCases.Moderation;

public static class ModerationQueueCursorCodec
{
    public const int CurrentVersion = 1;

    private const int MaxCursorLength = 512;

    private sealed record Payload(int Version, int TotalReports, string OldestReportAt, Guid ListingId);

    public static string Encode(int totalReports, DateTime oldestReportAt, Guid listingId)
    {
        var payload = new Payload(
            CurrentVersion,
            totalReports,
            oldestReportAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            listingId);
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload, ApiJson.Options));
    }

    public static bool TryDecode(string? value, out ModerationQueueCursor cursor)
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
            || payload.TotalReports < 1
            || payload.ListingId == Guid.Empty
            || !DateTime.TryParseExact(
                payload.OldestReportAt,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var oldestReportAt))
        {
            return false;
        }

        cursor = new ModerationQueueCursor(
            payload.TotalReports,
            oldestReportAt.ToUniversalTime(),
            payload.ListingId);
        return true;
    }
}
