namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>File/yêu cầu không hợp lệ -> HTTP 400.</summary>
public class PhotoValidationException(string message) : Exception(message);

/// <summary>Phòng không tồn tại -> HTTP 404.</summary>
public class ListingNotFoundException(string listingId)
    : Exception($"Không tìm thấy phòng '{listingId}'.");
