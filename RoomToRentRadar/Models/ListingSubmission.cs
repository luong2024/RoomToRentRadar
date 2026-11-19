using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Models;

/// <summary>
/// A photo that is about to be uploaded. The stream is opened on demand so a
/// failed request can be retried without buffering the whole file.
/// </summary>
public sealed record ListingPhotoUploadModel(
    string FileName,
    string ContentType,
    long? Length,
    Func<Task<Stream>> OpenReadAsync);

/// <summary>
/// Scalar values and photos sent to the landlord listing endpoints
/// (<c>POST api/v1/listings</c> and <c>PUT api/v1/listings/{id}</c>).
/// </summary>
public sealed record ListingSubmission(
    string Title,
    string Address,
    long Price,
    long Deposit,
    double AreaM2,
    RoomType RoomType,
    int Bedrooms,
    string? Description,
    double? Lat,
    double? Lng,
    string? LandlordName,
    string? LandlordPhone,
    IReadOnlyList<ListingPhotoUploadModel> Photos);
