using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// Thông tin ảnh phòng trọ gửi kèm yêu cầu đăng tin (US08 / T08.1 & T08.2).
/// </summary>
public sealed class CreateListingPhotoPayload
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "image/jpeg";

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("localPathOrUrl")]
    public string LocalPathOrUrl { get; set; } = string.Empty;

    [JsonPropertyName("is360")]
    public bool Is360 { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

/// <summary>
/// Nội quy phòng trọ đính kèm khi chủ trọ tạo tin mới (US08 / T08.1).
/// </summary>
public sealed class CreateListingHouseRulesPayload
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
/// Request gửi lên POST /api/v1/landlord/listings (US08 / T08.2).
/// </summary>
public sealed class CreateListingRequestContract
{
    [JsonPropertyName("campusId")]
    public string CampusId { get; set; } = "ictu-main";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("rent")]
    public decimal Rent { get; set; }

    [JsonPropertyName("deposit")]
    public decimal Deposit { get; set; }

    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("roomType")]
    public string RoomType { get; set; } = "Phòng khép kín";

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("amenities")]
    public List<string> Amenities { get; set; } = [];

    [JsonPropertyName("houseRules")]
    public CreateListingHouseRulesPayload HouseRules { get; set; } = new();

    [JsonPropertyName("landlordName")]
    public string LandlordName { get; set; } = string.Empty;

    [JsonPropertyName("landlordPhone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [JsonPropertyName("photos")]
    public List<CreateListingPhotoPayload> Photos { get; set; } = [];
}

/// <summary>
/// Response trả về từ POST /api/v1/landlord/listings với trạng thái PendingReview.
/// </summary>
public sealed class CreateListingResponseContract
{
    [JsonPropertyName("listingId")]
    public string ListingId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "PendingReview";

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("moderationStatus")]
    public string ModerationStatus { get; set; } = "PendingReview";

    [JsonPropertyName("submittedAt")]
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
