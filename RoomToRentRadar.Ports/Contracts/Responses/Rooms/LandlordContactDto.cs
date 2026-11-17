namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>LandlordContactDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record LandlordContactDto(
    string Id,
    string DisplayName,
    bool IsVerified,
    DateTime MemberSinceUtc,
    string? PhoneNumber,
    string? ZaloUrl
);