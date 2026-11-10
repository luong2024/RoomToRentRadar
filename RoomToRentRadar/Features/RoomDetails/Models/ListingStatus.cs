namespace RoomToRentRadar.Features.RoomDetails.Models;

public enum ListingStatus
{
    Available = 0,   // Phòng còn trống
    Rented = 1,      // Phòng đã được thuê
    Unavailable = 2  // Tin đăng không còn khả dụng
}