using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.Dtos;

public static class ListingModerationStatus
{
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class ListingAvailabilityStatus
{
    public const string Available = "Available";
    public const string Rented = "Rented";
    public const string PendingReview = "PendingReview";
    public const string Rejected = "Rejected";
    public const string Hidden = "Hidden";
    public const string Archived = "Archived";
}

/// <summary>Request duyệt hoặc từ chối tin đăng (PUT /api/v1/moderation/listings/{listingId}/status).</summary>
public sealed class ModerationDecisionRequest
{
    /// <summary>Hành động: "Approve" | "Approved" | "Reject" | "Rejected".</summary>
    [Required]
    public string Decision { get; set; } = string.Empty;

    /// <summary>Ghi chú hoặc lý do từ chối (bắt buộc khi Reject).</summary>
    public string? Note { get; set; }
}

/// <summary>Một bản ghi lịch sử kiểm duyệt (tương thích container ModerationReports).</summary>
public sealed class ModerationAuditLogDto
{
    public string Id { get; set; } = string.Empty;

    public string ListingId { get; set; } = string.Empty;

    public string ModeratorId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string PreviousModerationStatus { get; set; } = string.Empty;

    public string NewModerationStatus { get; set; } = string.Empty;

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Thông tin tin đăng hiển thị trong hàng đợi kiểm duyệt hoặc trang quản lý của chủ trọ.</summary>
public sealed class ModerationListingItemDto
{
    public string ListingId { get; set; } = string.Empty;

    public string CampusId { get; set; } = "ictu-main";

    public string Title { get; set; } = string.Empty;

    public string LandlordId { get; set; } = string.Empty;

    public string LandlordName { get; set; } = string.Empty;

    public decimal Rent { get; set; }

    public double Area { get; set; }

    public string RoomType { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    public string ModerationStatus { get; set; } = string.Empty;

    public int PhotoCount { get; set; }

    public string? LatestModerationNote { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Response của GET /api/v1/moderation/listings.</summary>
public sealed class ModerationQueueResponse
{
    public string ModerationStatusFilter { get; set; } = ListingModerationStatus.PendingReview;

    public int TotalCount { get; set; }

    public int PendingCount { get; set; }

    public int ApprovedCount { get; set; }

    public int RejectedCount { get; set; }

    public List<ModerationListingItemDto> Items { get; set; } = [];
}

/// <summary>Response sau khi Moderator duyệt hoặc từ chối tin đăng.</summary>
public sealed class ModerationDecisionResponse
{
    public string ListingId { get; set; } = string.Empty;

    public string PreviousModerationStatus { get; set; } = string.Empty;

    public string ModerationStatus { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset ModeratedAt { get; set; }

    public ModerationAuditLogDto AuditLog { get; set; } = new();
}

/// <summary>Response danh sách tin đăng của chủ trọ (GET /api/v1/landlord/listings).</summary>
public sealed class LandlordListingsResponse
{
    public string LandlordId { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public int PendingReviewCount { get; set; }

    public int AvailableCount { get; set; }

    public int RentedCount { get; set; }

    public int HiddenCount { get; set; }

    public int RejectedCount { get; set; }

    public List<ModerationListingItemDto> Items { get; set; } = [];
}

/// <summary>Request chủ trọ cập nhật trạng thái hiển thị phòng (Available | Rented | Hidden).</summary>
public sealed class UpdateListingAvailabilityRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

/// <summary>Response sau khi chủ trọ đổi trạng thái phòng.</summary>
public sealed class UpdateListingAvailabilityResponse
{
    public string ListingId { get; set; } = string.Empty;

    public string LandlordId { get; set; } = string.Empty;

    public string PreviousStatus { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ModerationStatus { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Response sau khi chủ trọ xóa/ẩn mềm tin đăng.</summary>
public sealed class ArchiveListingResponse
{
    public string ListingId { get; set; } = string.Empty;

    public string LandlordId { get; set; } = string.Empty;

    public string Status { get; set; } = ListingAvailabilityStatus.Archived;

    public bool IsVerified { get; set; }

    public DateTimeOffset ArchivedAt { get; set; }
}
