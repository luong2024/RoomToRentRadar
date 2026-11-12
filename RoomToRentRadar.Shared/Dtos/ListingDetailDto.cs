namespace RoomToRentRadar.Shared.Dtos;

public sealed record ListingDetailDto(
    string Id,
    string CampusId,
    string Title,
    string Description,
    string Status,
    bool IsVerified,
    bool IsAvailable,
    string? UnavailableMessage,
    decimal Rent,
    decimal Deposit,
    double Area,
    string RoomType,
    string Address,
    GeoJsonPointDto Location,
    IReadOnlyList<string> Amenities,
    double SecurityScore,
    IReadOnlyList<PhotoDto> Photos,
    HouseRulesDto HouseRules,
    LandlordDto Landlord,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ListingCardDto> SimilarRooms);
