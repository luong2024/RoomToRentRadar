namespace RoomToRentRadar.Api.Services.Favourites;

/// <summary>Tham số yêu cầu yêu thích không hợp lệ -> HTTP 400.</summary>
public sealed class FavouriteValidationException(string message) : Exception(message);

/// <summary>Phòng trọ không tồn tại khi thêm yêu thích -> HTTP 404.</summary>
public sealed class FavouriteListingNotFoundException(string listingId)
    : Exception($"Không tìm thấy phòng '{listingId}'.");
