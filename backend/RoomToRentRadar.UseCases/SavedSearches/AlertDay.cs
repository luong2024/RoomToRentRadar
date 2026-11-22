namespace RoomToRentRadar.UseCases.SavedSearches;

/// <summary>
/// Calendar-day boundaries for the daily alert cap. The product day is
/// Asia/Ho_Chi_Minh (UTC+7); Vietnam has no daylight saving, so a fixed offset
/// is exact.
/// </summary>
public static class AlertDay
{
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(7);

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcRange(DateTime utcNow)
    {
        var localStart = utcNow + UtcOffset;
        var startUtc = DateTime.SpecifyKind(localStart.Date - UtcOffset, DateTimeKind.Utc);
        return (startUtc, startUtc.AddDays(1));
    }
}
