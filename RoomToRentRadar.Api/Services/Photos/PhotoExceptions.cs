namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>Dữ liệu client gửi lên không hợp lệ -> HTTP 400.</summary>
public class PhotoValidationException(string message) : Exception(message);

/// <summary>Phòng trọ không tồn tại -> HTTP 404.</summary>
public class RoomNotFoundException(string roomId) : Exception($"Không tìm thấy phòng trọ '{roomId}'.");
