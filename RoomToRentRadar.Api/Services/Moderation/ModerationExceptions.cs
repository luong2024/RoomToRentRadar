namespace RoomToRentRadar.Api.Services.Moderation;

/// <summary>Yêu cầu kiểm duyệt / quản lý tin đăng không hợp lệ -> HTTP 400.</summary>
public sealed class ListingManagementValidationException(string message) : Exception(message);

/// <summary>Không có quyền thao tác lên tin đăng hoặc hàng đợi kiểm duyệt -> HTTP 403.</summary>
public sealed class ListingManagementForbiddenException(string message) : Exception(message);

/// <summary>Không tìm thấy tin đăng -> HTTP 404.</summary>
public sealed class ListingManagementNotFoundException(string listingId)
    : Exception($"Không tìm thấy phòng '{listingId}'.");
