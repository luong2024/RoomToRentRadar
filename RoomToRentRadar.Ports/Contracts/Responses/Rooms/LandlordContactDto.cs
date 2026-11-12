namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Landlord public contact and trust indicators per docs/api/openapi.yaml schema LandlordContactDto and BR-AUTH-07.
/// PhoneNumber and ZaloUrl are null for anonymous callers.
/// </summary>
public sealed record LandlordContactDto(
    string Id,
    string DisplayName,
    bool IsVerified,
    DateTime MemberSinceUtc,
    string? PhoneNumber,
    string? ZaloUrl
);
