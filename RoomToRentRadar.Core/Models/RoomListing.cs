using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Core rental room entity covering Sprint 1 stories (US02 -> US12).
    /// </summary>
    public class RoomListing
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public decimal Deposit { get; set; }
        public double Area { get; set; }
        public string RoomType { get; set; } = "Phòng khép kín"; // Phòng khép kín, Chung cư mini, Ở ghép
        public string Address { get; set; } = string.Empty;
        public string Ward { get; set; } = "Quyết Thắng";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceToCampusKm { get; set; }

        // US10: Travel time estimates (minutes)
        public int MotorbikeMinutes { get; set; }
        public int BusMinutes { get; set; }
        public int WalkMinutes { get; set; }

        // US03 & US04: Media & Amenities
        public string AmenitiesCsv { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = "dotnet_bot.png";
        public string PhotosJson { get; set; } = "[]";
        public string Panorama360Url { get; set; } = string.Empty;

        // Landlord & Verification (US02, US03, US09)
        public int LandlordId { get; set; }
        public string LandlordName { get; set; } = string.Empty;
        public string LandlordPhone { get; set; } = string.Empty;
        public string Status { get; set; } = "Available"; // Available, PendingReview, Unavailable
        public bool IsVerified { get; set; }

        // US11: Security score (out of 5.0)
        public double SecurityScore { get; set; } = 4.5;

        // US12: House rules & versioning
        public string QuietHours { get; set; } = "22:30 - 05:30";
        public string GuestPolicy { get; set; } = "Khách đến chơi báo trước, không ở qua đêm sau 22:30";
        public string CookingRules { get; set; } = "Được nấu ăn bằng bếp từ hoặc bếp hồng ngoại";
        public string ParkingRules { get; set; } = "Mỗi phòng tối đa 2 xe máy tại nhà xe có camera";
        public int RulesVersion { get; set; } = 1;

        public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        [JsonIgnore]
        public bool IsFavorite { get; set; }

        [JsonIgnore]
        public string MaskedLandlordPhone =>
            string.IsNullOrWhiteSpace(LandlordPhone) || LandlordPhone.Length < 7
                ? "09xx xxx xxx"
                : $"{LandlordPhone[..4]} *** {LandlordPhone[^3..]}";
    }
}
