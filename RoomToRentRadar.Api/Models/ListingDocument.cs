namespace RoomToRentRadar.Api.Models;

public sealed class ListingDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CampusId { get; set; } = "ictu-main";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Available";

    public bool IsVerified { get; set; }

    public string ModerationStatus { get; set; } = "Approved";

    public decimal Rent { get; set; }

    public decimal Deposit { get; set; }

    public double Area { get; set; }

    public string RoomType { get; set; } = "Studio";

    public string Address { get; set; } = string.Empty;

    public double DistanceMeters { get; set; }

    public GeoJsonPoint Location { get; set; } = new();

    public List<string> Amenities { get; set; } = [];

    public double SecurityScore { get; set; }

    public List<PhotoDocument> Photos { get; set; } = [];

    public HouseRulesDocument HouseRules { get; set; } = new();

    public LandlordDocument Landlord { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GeoJsonPoint
{
    public string Type { get; set; } = "Point";

    public double[] Coordinates { get; set; } = [105.8066, 21.5850];
}

public sealed class PhotoDocument
{
    public string Id { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public bool Is360 { get; set; }

    public int Order { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public long SizeBytes { get; set; }
}

public sealed class HouseRulesDocument
{
    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string QuietHours { get; set; } = string.Empty;

    public string GuestPolicy { get; set; } = string.Empty;

    public string CookingRules { get; set; } = string.Empty;

    public string ParkingRules { get; set; } = string.Empty;
}

public sealed class LandlordDocument
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}
