using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// Body gửi lên PUT /api/v1/listings/{id}/house-rules (US11 / T11.1).
/// Khớp UpdateHouseRulesDto trong tài liệu Backend.
/// </summary>
public sealed class UpdateHouseRulesRequestContract
{
    [JsonPropertyName("quietHours")]
    public string QuietHours { get; set; } = string.Empty;

    [JsonPropertyName("guestPolicy")]
    public string GuestPolicy { get; set; } = string.Empty;

    [JsonPropertyName("cookingRules")]
    public string CookingRules { get; set; } = string.Empty;

    [JsonPropertyName("parkingRules")]
    public string ParkingRules { get; set; } = string.Empty;
}

/// <summary>
/// Ảnh chụp một phiên bản nội quy cũ được lưu trong lịch sử (T11.1 & T11.2).
/// </summary>
public sealed class HouseRulesVersionSnapshotContract
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("quietHours")]
    public string QuietHours { get; set; } = string.Empty;

    [JsonPropertyName("guestPolicy")]
    public string GuestPolicy { get; set; } = string.Empty;

    [JsonPropertyName("cookingRules")]
    public string CookingRules { get; set; } = string.Empty;

    [JsonPropertyName("parkingRules")]
    public string ParkingRules { get; set; } = string.Empty;
}

/// <summary>
/// Response trả về từ PUT /api/v1/listings/{id}/house-rules kèm version mới và history.
/// </summary>
public sealed class HouseRulesResponseContract
{
    [JsonPropertyName("listingId")]
    public string ListingId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("quietHours")]
    public string QuietHours { get; set; } = string.Empty;

    [JsonPropertyName("guestPolicy")]
    public string GuestPolicy { get; set; } = string.Empty;

    [JsonPropertyName("cookingRules")]
    public string CookingRules { get; set; } = string.Empty;

    [JsonPropertyName("parkingRules")]
    public string ParkingRules { get; set; } = string.Empty;

    [JsonPropertyName("history")]
    public List<HouseRulesVersionSnapshotContract> History { get; set; } = [];
}
