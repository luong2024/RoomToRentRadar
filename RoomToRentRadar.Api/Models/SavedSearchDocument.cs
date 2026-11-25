namespace RoomToRentRadar.Api.Models;

/// <summary>
/// Document trong container <c>UsersInteractions</c> (Partition Key: <c>/userId</c>, <c>type = "SavedSearch"</c>) — US07 / T07.1.
/// </summary>
public sealed class SavedSearchDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Partition key.</summary>
    public string UserId { get; set; } = string.Empty;

    public string Type { get; set; } = "SavedSearch";

    public string Name { get; set; } = string.Empty;

    public string CampusId { get; set; } = "ictu-main";
    public decimal? MinRent { get; set; }
    public decimal? MaxRent { get; set; }
    public double? MinArea { get; set; }
    public double? MaxArea { get; set; }
    public string? RoomType { get; set; }
    public double? MaxDistanceMeters { get; set; }

    /// <summary>GeoJSON Polygon ring [lng, lat] đã được đóng; null = không giới hạn vùng.</summary>
    public List<double[]>? Polygon { get; set; }

    public bool AlertsEnabled { get; set; } = true;

    /// <summary>Bộ đếm alert theo ngày (US07 AC4) — chỉ có hiệu lực khi <see cref="AlertCounterDate"/> là hôm nay.</summary>
    public int AlertsSentToday { get; set; }

    /// <summary>Ngày (giờ Việt Nam) của bộ đếm <see cref="AlertsSentToday"/>.</summary>
    public DateOnly? AlertCounterDate { get; set; }

    public DateTimeOffset? LastAlertAt { get; set; }

    /// <summary>Các listing đã alert cho saved search này — tránh gửi trùng một phòng hai lần.</summary>
    public List<string> AlertedListingIds { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
