using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Domain.ValueObjects;

/// <summary>
/// Editable listing fields accepted by <c>Room.UpdateDetails</c> (BR-LST-05, BR-LST-09).
/// Reuses the string columns of <c>docs/erd.md</c> §4.4 for furnishing / water price unit so this
/// input stays backward compatible with the aggregate's public surface.
/// </summary>
public sealed record RoomDetails(
    string Title,
    string? Description,
    RoomType RoomType,
    long MonthlyRentVnd,
    long DepositVnd,
    double AreaM2,
    int? MaxOccupants,
    string? Furnishing,
    DateOnly? AvailableFrom,
    string AddressLine,
    string Ward,
    string District,
    double Latitude,
    double Longitude,
    long? ElectricityPriceVndPerKwh,
    long? WaterPriceVnd,
    string? WaterPriceUnit,
    long? InternetFeeVnd,
    long? ParkingFeeVnd,
    string? OtherFeesNote,
    IReadOnlyList<Amenity> Amenities,
    IReadOnlyList<SafetyFeature> SafetyFeatures,
    bool HasAddedNewPhotos);
