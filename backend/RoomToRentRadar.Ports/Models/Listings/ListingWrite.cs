using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Listings;

public sealed record ListingWriteCommand(
    string Title,
    string Address,
    long Price,
    long Deposit,
    double AreaM2,
    RoomType RoomType,
    string? Description,
    int? Bedrooms,
    double? Lat,
    double? Lng,
    string? LandlordName,
    string? LandlordPhone,
    IReadOnlyList<PhotoUpload> Photos);

public sealed record HouseRulesInput(
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules);

public sealed record ListingWriteError(
    string Code,
    IReadOnlyDictionary<string, List<string>>? ValidationErrors = null);

public sealed record ListingWriteResult<T>(T? Value, ListingWriteError? Error)
    where T : class
{
    public bool IsSuccess => Error is null;

    public static ListingWriteResult<T> Success(T value) => new(value, null);

    public static ListingWriteResult<T> Failure(
        string code,
        IReadOnlyDictionary<string, List<string>>? validationErrors = null)
        => new(null, new ListingWriteError(code, validationErrors));
}

public static class ListingWriteErrorCodes
{
    public const string NotFound = "listing_not_found";
    public const string TooFewPhotos = "too_few_photos";
    public const string InvalidPhoto = "invalid_listing_photo";
    public const string PhotoLimitExceeded = "photo_limit_exceeded";
    public const string InvalidListingTransition = "invalid_listing_transition";
    public const string InvalidHouseRules = "invalid_house_rules";
}
