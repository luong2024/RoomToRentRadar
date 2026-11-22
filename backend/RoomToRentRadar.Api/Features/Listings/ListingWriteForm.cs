using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Listings;

/// <summary>
/// Multipart form fields accepted by the landlord listing endpoints. Optional
/// fields stay nullable so an update can keep the stored value when they are
/// not sent.
/// </summary>
public sealed class ListingWriteForm
{
    [FromForm(Name = "title")]
    public string? Title { get; set; }

    [FromForm(Name = "address")]
    public string? Address { get; set; }

    [FromForm(Name = "price")]
    public long? Price { get; set; }

    [FromForm(Name = "deposit")]
    public long? Deposit { get; set; }

    [FromForm(Name = "area_m2")]
    public double? AreaM2 { get; set; }

    [FromForm(Name = "room_type")]
    public string? RoomType { get; set; }

    [FromForm(Name = "bedrooms")]
    public int? Bedrooms { get; set; }

    [FromForm(Name = "description")]
    public string? Description { get; set; }

    [FromForm(Name = "lat")]
    public double? Lat { get; set; }

    [FromForm(Name = "lng")]
    public double? Lng { get; set; }

    [FromForm(Name = "landlord_name")]
    public string? LandlordName { get; set; }

    [FromForm(Name = "landlord_phone")]
    public string? LandlordPhone { get; set; }

    [FromForm(Name = "photos")]
    public List<IFormFile>? Photos { get; set; }
}
