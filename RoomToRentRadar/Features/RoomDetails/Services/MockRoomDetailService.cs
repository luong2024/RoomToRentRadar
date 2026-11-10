using RoomToRentRadar.Features.RoomDetails.Models;

namespace RoomToRentRadar.Features.RoomDetails.Services;

public class MockRoomDetailService : IRoomDetailService
{
    public Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default)
    {
        // Giả lập dữ liệu theo từng trạng thái để bạn test
        var status = roomId switch
        {
            "rented" => ListingStatus.Rented,
            "unavailable" => ListingStatus.Unavailable,
            _ => ListingStatus.Available
        };

        var room = new RoomDetailModel
        {
            Id = roomId,
            Title = "Phòng trọ cao cấp gần ĐH CNTT & TT (ICTU)",
            Description = "Phòng trọ khép kín mới xây, thoáng mát, an ninh tốt, giờ giấc tự do, có chỗ để xe rộng rãi, camera an ninh 24/7. Rất phù hợp cho sinh viên ICTU.",
            Price = 1800000,
            Deposit = 1000000,
            AreaSquareMeters = 22,
            RoomType = "Phòng trọ đơn khép kín",
            Address = "Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên (Cách cổng ICTU 300m)",
            Status = status,
            LastUpdated = DateTime.Now.AddDays(-2),
            LandlordName = "Nguyễn Văn An",
            LandlordPhone = "0987654321",
            LandlordEmail = "chutro.an@gmail.com",
            LandlordAvatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150",
            ImageUrls =
            [
                "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=800",
                "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800",
                "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=800"
            ],
            Amenities =
            [
                new() { Name = "Điều hòa", IconGlyph = "\uf108" },
                new() { Name = "Bình nóng lạnh", IconGlyph = "\uf8fe" },
                new() { Name = "Wifi tốc độ cao", IconGlyph = "\uf871" },
                new() { Name = "Camera 24/7", IconGlyph = "\uf837" },
                new() { Name = "Chỗ để xe", IconGlyph = "\uf2a3" },
                new() { Name = "Giờ giấc tự do", IconGlyph = "\uf2c6" }
            ],
            HouseRules =
            [
                "Không làm ồn sau 23:00",
                "Giữ gìn vệ sinh chung khu hành lang",
                "Không nuôi thú cưng gây ồn",
                "Đóng tiền phòng trước ngày mùng 5 hàng tháng"
            ]
        };

        return Task.FromResult<RoomDetailModel?>(room);
    }

    public Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var list = new List<SimilarRoomModel>
        {
            new()
            {
                Id = "sim-1",
                Title = "Phòng khép kín ban công ICTU",
                Price = 1700000,
                AreaSquareMeters = 20,
                Address = "Ngõ 1 Đường Z115, Thái Nguyên",
                ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=400"
            },
            new()
            {
                Id = "sim-2",
                Title = "Phòng trọ sinh viên gần cổng trường",
                Price = 1600000,
                AreaSquareMeters = 18,
                Address = "Đường Z115, Thái Nguyên",
                ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=400"
            }
        };

        return Task.FromResult(list);
    }
}