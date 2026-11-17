namespace RoomToRentRadar.Domain.Common;

/// <summary>
/// Provides unified business date/time calculations aligned with Vietnam business timezone (Asia/Ho_Chi_Minh, UTC+7).
/// Prevents cross-timezone edge cases where early morning hours in Vietnam (00:00 to 07:00 ICT)
/// fall on the previous calendar day in UTC.
/// </summary>
public static class BusinessTime
{
    public const string VietnamTimeZoneId = "Asia/Ho_Chi_Minh";
    public static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneId);
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Indochina Time", "Indochina Time");
            }
        }
    }

    /// <summary>
    /// Gets the business calendar date (DateOnly) in Vietnam timezone for a given timestamp.
    /// </summary>
    public static DateOnly ToBusinessDate(DateTimeOffset dateTimeOffset)
    {
        var localDateTime = TimeZoneInfo.ConvertTime(dateTimeOffset, VietnamTimeZone);
        return DateOnly.FromDateTime(localDateTime.DateTime);
    }
}
