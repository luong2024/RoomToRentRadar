using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingWriteInput(
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
    IReadOnlyList<IFormFile> Photos);
