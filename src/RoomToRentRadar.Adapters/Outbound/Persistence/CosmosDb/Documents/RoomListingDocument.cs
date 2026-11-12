namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

using Newtonsoft.Json;

public sealed class RoomListingDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("campusId")]
    public string CampusId { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("monthlyRentAmount")]
    public decimal MonthlyRentAmount { get; set; }

    [JsonProperty("monthlyRentCurrency")]
    public string MonthlyRentCurrency { get; set; } = "VND";

    [JsonProperty("areaSquareMeters")]
    public double AreaSquareMeters { get; set; }

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("latitude")]
    public double Latitude { get; set; }

    [JsonProperty("longitude")]
    public double Longitude { get; set; }

    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("verificationStatus")]
    public int VerificationStatus { get; set; }

    [JsonProperty("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }
}
