namespace RoomToRentRadar.Shared.Dtos;

/// <summary>
/// Bộ lọc được lưu cùng saved search (US07 / T07.1).
/// Cùng tên thuộc tính với <see cref="ListingSearchQuery"/> để MAUI (T04.2 / T05.3) map thẳng từ filter panel.
/// </summary>
public sealed class SavedSearchFiltersDto
{
    public string CampusId { get; set; } = "ictu-main";
    public decimal? MinRent { get; set; }
    public decimal? MaxRent { get; set; }
    public double? MinArea { get; set; }
    public double? MaxArea { get; set; }
    public string? RoomType { get; set; }
    public double? MaxDistanceMeters { get; set; }
}

/// <summary>Body cho POST /api/v1/saved-searches và PUT /api/v1/saved-searches/{id}.</summary>
public sealed class SaveSearchRequestDto
{
    /// <summary>Tên hiển thị, ví dụ "Khu Z115 &lt; 2tr". Để trống =&gt; server tự đặt tên.</summary>
    public string? Name { get; set; }

    public SavedSearchFiltersDto Filters { get; set; } = new();

    /// <summary>Vùng vẽ trên bản đồ (GeoJSON ring [lng, lat]); null/rỗng = không giới hạn vùng.</summary>
    public List<double[]>? Polygon { get; set; }

    public bool AlertsEnabled { get; set; } = true;
}

/// <summary>Body cho PATCH /api/v1/saved-searches/{id}/alerts.</summary>
public sealed class ToggleSavedSearchAlertsRequestDto
{
    public bool AlertsEnabled { get; set; }
}

public sealed class SavedSearchDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SavedSearchFiltersDto Filters { get; set; } = new();
    public List<double[]>? Polygon { get; set; }
    public bool AlertsEnabled { get; set; }

    /// <summary>Số alert đã gửi hôm nay (theo giờ Việt Nam, reset 00:00 UTC+7).</summary>
    public int AlertsSentToday { get; set; }

    /// <summary>Giới hạn alert/ngày cho mỗi saved search (US07 AC4) = 3.</summary>
    public int MaxAlertsPerDay { get; set; }

    public DateTimeOffset? LastAlertAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Kết quả GET /api/v1/saved-searches/{id}/matches — chạy lại saved search ngay lập tức.</summary>
public sealed class SavedSearchMatchesDto
{
    public string SavedSearchId { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<SpatialSearchPinDto> Items { get; set; } = [];
}

/// <summary>
/// Một alert cần gửi, do rule engine T07.1 trả về cho push worker T07.2 (Hiếu).
/// </summary>
public sealed class SavedSearchAlertDto
{
    public string SavedSearchId { get; set; } = string.Empty;
    public string SavedSearchName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ListingId { get; set; } = string.Empty;
    public string ListingTitle { get; set; } = string.Empty;
    public decimal Rent { get; set; }

    /// <summary>Số alert còn được gửi hôm nay sau alert này.</summary>
    public int RemainingAlertsToday { get; set; }
}
