using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.ListingManagement.Models;

/// <summary>
/// Wire field names used by both the client validator and the API
/// <c>invalid_listing</c> problem, so one mapping covers both sources.
/// </summary>
public static class ListingFieldKeys
{
    public const string Title = "title";
    public const string Address = "address";
    public const string Price = "price";
    public const string Deposit = "deposit";
    public const string Area = "area_m2";
    public const string RoomType = "room_type";
    public const string Bedrooms = "bedrooms";
    public const string Description = "description";
    public const string Lat = "lat";
    public const string Lng = "lng";
    public const string LandlordName = "landlord_name";
    public const string LandlordPhone = "landlord_phone";
    public const string Photos = "photos";
}

/// <summary>A photo that is about to be uploaded, as the validator sees it.</summary>
public sealed record ListingPhotoCandidate(string FileName, string? ContentType, long? Length);

/// <summary>
/// Raw editor values as typed by the landlord. Plain data so validation stays
/// free of MAUI types.
/// </summary>
public sealed class ListingFormState
{
    public string? Title { get; set; }

    public string? Address { get; set; }

    public string? PriceText { get; set; }

    public string? DepositText { get; set; }

    public string? AreaText { get; set; }

    public string? BedroomsText { get; set; }

    public string? DescriptionText { get; set; }

    public string? LatText { get; set; }

    public string? LngText { get; set; }

    public string? LandlordNameText { get; set; }

    public string? LandlordPhoneText { get; set; }

    public RoomType? RoomType { get; set; }

    /// <summary>Photos already stored on the server (read-only in the editor).</summary>
    public int ExistingPhotoCount { get; set; }

    public IReadOnlyList<ListingPhotoCandidate> NewPhotos { get; set; } = [];
}
