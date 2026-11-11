namespace RoomToRentRadar.Api.Models;

/// <summary>
/// Document phòng trọ lưu trong Azure Cosmos DB (container "Listings", partition key "/city").
/// </summary>
public class Listing
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Giá thuê theo tháng (VND).</summary>
    public decimal Price { get; set; }

    /// <summary>Diện tích (m²).</summary>
    public double Area { get; set; }

    public string Address { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    /// <summary>Partition key.</summary>
    public string City { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = [];

    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
