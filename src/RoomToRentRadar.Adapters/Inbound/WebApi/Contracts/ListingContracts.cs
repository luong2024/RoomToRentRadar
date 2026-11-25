namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed class ListingFormRequest
{
    public string? Title { get; set; }
    public string? Address { get; set; }
    public decimal MonthlyRentVnd { get; set; }
    public decimal DepositVnd { get; set; }
    public double AreaM2 { get; set; }
    public string? RoomType { get; set; }
    public string? Description { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public List<Microsoft.AspNetCore.Http.IFormFile> Photos { get; set; } = [];
    public List<string>? ImageUrlsToRetain { get; set; }
    /// <summary>On update, replace all existing photos with the uploaded photos.</summary>
    public bool ReplaceGallery { get; set; }
}

public sealed record ListingWriteResponse(
    string ListingId,
    string Id,
    string Status,
    IReadOnlyList<string> ImageUrls,
    double? Latitude = null,
    double? Longitude = null);

public sealed record ListingCoordinatesResponse(string ListingId, double? Latitude, double? Longitude);
