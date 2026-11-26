namespace RoomToRentRadar.Api.Services.Time;

/// <summary>Giờ Việt Nam (UTC+7, không có DST) — dùng chung cho bộ đếm alert và tên mặc định.</summary>
public static class VietnamTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTimeOffset ToVietnamTime(this DateTimeOffset instant) => instant.ToOffset(Offset);

    /// <summary>Ngày hiện tại theo giờ Việt Nam (bộ đếm alert reset lúc 00:00 VN).</summary>
    public static DateOnly GetVietnamToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().ToVietnamTime().DateTime);
}
