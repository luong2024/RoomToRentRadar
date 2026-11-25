namespace RoomToRentRadar.Models.RoomDetails;

public class RoomDetailModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? Deposit { get; set; }
    public double AreaSquareMeters { get; set; }
    public string RoomType { get; set; } = "Private room";
    public string Address { get; set; } = string.Empty;
    public ListingStatus Status { get; set; } = ListingStatus.Available;
    public DateTime? LastUpdated { get; set; }

    public bool IsVerified { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableMessage { get; set; }

    // Các thông số phòng
    public int? SittingRooms { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? Kitchens { get; set; }

    // Đánh giá sao
    public double? Rating { get; set; }
    public int? ReviewCount { get; set; }

    // Thông tin chủ trọ
    public string LandlordName { get; set; } = "Landlord information unavailable";
    public string? LandlordPhone { get; set; }
    public string? LandlordEmail { get; set; }
    public string? LandlordAvatar { get; set; }

    public List<string> ImageUrls { get; set; } = [];
    public bool HasImages => ImageUrls.Count > 0;
    public bool HasBedrooms => Bedrooms.HasValue;
    public bool HasBathrooms => Bathrooms.HasValue;
    public bool HasLastUpdated => LastUpdated.HasValue;
    public List<RoomAmenityModel> Amenities { get; set; } = [];
    public List<string> HouseRules { get; set; } = [];
    public RoomHouseRulesModel? StructuredHouseRules { get; set; }
    public List<SimilarRoomModel> SimilarRooms { get; set; } = [];

    // Helper tính toán & định dạng
    public string FormattedPrice => $"₫{Price:N0}/month";
    public string FormattedDeposit => Deposit is decimal deposit ? $"{deposit:N0} ₫" : "Not provided";
    public string FormattedArea => $"{AreaSquareMeters:0.#} m²";
    public string FormattedLastUpdated => LastUpdated is DateTime lastUpdated
        ? $"Updated: {lastUpdated:dd/MM/yyyy}"
        : "";
    public string RatingDisplay => Rating is double rating ? rating.ToString("0.0") : "No ratings yet";
    public decimal? TotalPackage => Deposit is decimal deposit ? Price + deposit : null;
    public string FormattedTotalPackage => TotalPackage is decimal totalPackage ? $"{totalPackage:N0} ₫" : "Not provided";
}
